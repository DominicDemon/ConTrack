using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Support;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace ContractManagement.API.Internal;

public static class InternalHost
{
    internal const string UserKey = "Internal.EmployeeId.v1";
    private sealed class CompanySigningAction { }
    public static async Task Run(WebApplicationBuilder builder, string[] args)
    {
        if (builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
        {
            // The default Windows EventLog provider can throw when a local developer
            // cannot write to the .NET Runtime event source, aborting requests while
            // Data Protection reports an ordinary development warning.
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
        }
        var masterConn = builder.Configuration.GetConnectionString("MasterDatabase");
        if (string.IsNullOrWhiteSpace(masterConn))
        {
            masterConn = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration.GetConnectionString("CentralDatabase");
            if (!string.IsNullOrWhiteSpace(masterConn))
            {
                builder.Configuration["ConnectionStrings:MasterDatabase"] = masterConn;
            }
        }
        if (string.IsNullOrWhiteSpace(masterConn))
            throw new InvalidOperationException("Internal mode requires ConnectionStrings:MasterDatabase.");
        builder.Services.AddMasterDatabase(builder.Configuration);
        builder.Services.AddScoped<InternalRepository>();
        builder.Services.AddScoped<DocumentCategories>();
        builder.Services.AddScoped<ContractHardDelete>();
        builder.Services.AddScoped<ContractFormatSettings>();
        builder.Services.AddScoped<ContractNumberRules>();
        builder.Services.AddScoped<IInternalRepository>(services => services.GetRequiredService<InternalRepository>());
        builder.Services.AddScoped<InternalDraftService>();
        builder.Services.AddScoped<IInternalTemplateImport, InternalTemplateImport>();
        builder.Services.AddScoped<InternalWordDocument>();
        builder.Services.AddScoped<InternalWordTemplates>();
        builder.Services.AddScoped<InternalApprovalService>();
        builder.Services.AddScoped<ContractProgressService>();
        builder.Services.AddScoped<IEmployeeAdmin, EmployeeAdminRepository>();
        builder.Services.AddScoped<EmployeeProfileService>();
        builder.Services.AddSingleton<EmailChangeCodes>();
        builder.Services.AddScoped<CustomerProfileService>();
        builder.Services.AddScoped<ContractManagement.Infrastructure.Email.CustomerWelcomeMailer>();
        builder.Services.AddSingleton<CustomerProfileGuards>();
        builder.Services.AddScoped<ICatalogAdmin, CatalogRepository>();
        builder.Services.AddSingleton<ISupportTicketService, SupportTicketService>();
        builder.Services.AddScoped<IContractReviewRepository, ContractReviewRepository>();
        builder.Services.AddScoped<IInternalSigningService, InternalSigningService>();
        builder.Services.AddScoped<ICustomerSigningService, CustomerSigningService>();
        builder.Services.AddScoped<IElectronicSigningPdfService, ContractManagement.Infrastructure.Files.ElectronicSigningPdfService>();
        builder.Services.AddScoped<ICustomerPortalRepository, CustomerPortalRepository>();
        builder.Services.AddSingleton<CustomerProfileGuards>();
        builder.Services.AddScoped<CustomerProfileService>();
        builder.Services.AddScoped<SmsCallbackProcessor>();
        builder.Services.AddOptions<EsmsOptions>().Bind(builder.Configuration.GetSection("Esms"))
            .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.ApiKey) && !string.IsNullOrWhiteSpace(o.SecretKey)
                && !string.IsNullOrWhiteSpace(o.Brandname) && Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps),
                "Enabled eSMS requires API key, secret, brandname and an HTTPS base URL.")
            .ValidateOnStart();
        var dataProtection = builder.Services.AddDataProtection();
        // Keep local internal sessions isolated from legacy keys protected for a different
        // Windows identity. Reusing those keys can make Kestrel abort every session request.
        if (builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
        {
            var keyDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".local", "internal-data-protection-dev"));
            Directory.CreateDirectory(keyDirectory);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        }
        builder.Services.AddSingleton<SmsOutboxWriter>();
        if (builder.Configuration.GetValue<bool>("Esms:Enabled")) builder.Services.AddHttpClient<ISmsSender, EsmsSmsSender>(h => h.Timeout = TimeSpan.FromSeconds(30));
        else builder.Services.AddSingleton<ISmsSender, FakeSmsSender>();
        builder.Services.AddHostedService<SmsOutboxWorker>();
        builder.Services.AddOptions<SmtpEmailOptions>().Bind(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));
        builder.Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        builder.Services.AddScoped<ContractExpiryReminders>();
        builder.Services.AddScoped<ContractNotificationFeed>();
        builder.Services.AddScoped<CustomerContractMailer>();
        builder.Services.AddHostedService<ContractExpiryReminderWorker>();
        builder.Services.AddScoped<ReviewLinkMailer>();
        builder.Services.AddScoped<ICustomerOtpMail, CustomerOtpMailer>();
        builder.Services.AddScoped<ReviewLinkOtp>();
        builder.Services.AddMemoryCache();
        ScanEndpoints.Register(builder.Services, builder.Configuration);
        // Contract PDFs, draft Word copies and templates are encrypted at rest (ContractFileCipher); scans have their own keys.
        var templateRoot = InternalTemplateStorage.ResolveRoot(builder.Configuration["InternalStorage:Root"], builder.Environment.ContentRootPath, AppContext.BaseDirectory);
        builder.Services.AddSingleton(sp => new ContractFileCipher(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContractManagement.Infrastructure.Persistence.Master.Scans.FileEncryptionOptions>>(),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetService<ContractManagement.Infrastructure.Persistence.Master.Scans.IRecoveryPublicKey>()));
        builder.Services.AddSingleton(sp => new InternalTemplateStorage(templateRoot, sp.GetRequiredService<ContractFileCipher>()));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new StringLongConverter()));
        builder.Services.AddDistributedMemoryCache();
        var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        builder.Services.AddSession(o => { o.Cookie.Name = ".ConTrack.Internal"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = secure; o.IdleTimeout = TimeSpan.FromHours(8); });
        // Customers get their own cookie; a request is served by exactly one of the two sessions, chosen by path.
        var customerSession = new SessionOptions { IdleTimeout = TimeSpan.FromHours(2) };
        customerSession.Cookie.Name = ".ConTrack.Customer"; customerSession.Cookie.HttpOnly = true; customerSession.Cookie.SameSite = SameSiteMode.Strict;
        customerSession.Cookie.SecurePolicy = secure; customerSession.Cookie.IsEssential = true;
        builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = ".ConTrack.Internal.Csrf"; o.Cookie.SameSite = SameSiteMode.Strict; });
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("internal-login", context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new() {
                PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("customer-login", context => RateLimitPartition.GetFixedWindowLimiter("customer:" + (context.Connection.RemoteIpAddress?.ToString() ?? "local"), _ => new() {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("public-signing", context => RateLimitPartition.GetFixedWindowLimiter("public-signing:" + (context.Connection.RemoteIpAddress?.ToString() ?? "local"), _ => new() {
                PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
        // Program.cs returns into this host before its Swagger registration, so Internal mode registers its own.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            o.CustomSchemaIds(t => t.FullName!.Replace('+', '.'));
            o.AddSecurityDefinition("csrf", new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-CSRF-TOKEN",
                Description = "Gọi GET /api/internal/auth/csrf (hoặc /api/customer/auth/csrf), dán giá trị token vào đây. Session nằm trong cookie của trình duyệt." });
            o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("csrf", doc)] = [] });
        });
        var app = builder.Build();
        if (RecoveryCommands.IsCommand(args))
        {
            Environment.ExitCode = await RecoveryCommands.RunAsync(args, app.Services);
            return;
        }
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (ContractManagement.Domain.Internal.DraftValidationException e) { context.Response.StatusCode = 422; await context.Response.WriteAsJsonAsync(new { message = e.Message, code = "draft_validation", issues = e.Issues }); }
            catch (InternalAppException e) { context.Response.StatusCode = e.Status; await context.Response.WriteAsJsonAsync(new { message = e.Message, code = e.Code }); }
            catch (ArgumentException e) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            catch (BadHttpRequestException e) { app.Logger.LogWarning(e, "Bad HTTP request: {Message}", e.Message); context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Phiên bảo vệ đã hết hạn. Tải lại trang và thử lại." }); }
            catch (DbUpdateException e) { app.Logger.LogWarning(e, "Internal database write failed"); context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu trùng hoặc đã thay đổi. Kiểm tra rồi thử lại." }); }
            catch (Exception e) when (!context.RequestAborted.IsCancellationRequested)
            {
                app.Logger.LogError(e, "Internal request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 500;
                var message = HttpMethods.IsGet(context.Request.Method)
                    ? "Không thể tải dữ liệu yêu cầu. Vui lòng thử lại."
                    : "Không thể xác nhận kết quả lưu. Hãy kiểm tra hồ sơ trước khi gửi lại.";
                await context.Response.WriteAsJsonAsync(new { message });
            }
        });
        if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        static bool IsCustomer(HttpContext c) => c.Request.Path.StartsWithSegments("/api/customer") || c.Request.Path.StartsWithSegments("/public/electronic-signing");
        app.UseWhen(IsCustomer, b => b.UseSession(customerSession));
        app.UseWhen(c => !IsCustomer(c), b => b.UseSession());
        app.UseRateLimiter();
        app.MapGet("/health/master", async (MasterDbContext db, CancellationToken ct) =>
        {
            await db.ContractDocuments.AsNoTracking().Take(1).ToListAsync(ct);
            await db.Employees.AsNoTracking().Take(1).ToListAsync(ct);
            return Results.Ok(new { status = "Ready", mode = "Internal" });
        });
        var api = app.MapGroup("/api/internal");
        api.MapGet("/auth/csrf", (HttpContext context, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken }));
        api.MapPost("/auth/login", async (LoginInput input, HttpContext context, IAntiforgery csrf, IInternalRepository repository, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(context);
            if (string.IsNullOrWhiteSpace(input.Username) || input.Username.Length > 100 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 200)
                throw new InternalAppException(401, "Tên đăng nhập hoặc mật khẩu không đúng.");
            var user = await repository.Authenticate(input.Username.Trim(), input.Password, ct);
            // Any internal role gets in; each screen checks its own permission.
            if (user is null || user.Permissions.Length == 0) throw new InternalAppException(401, "Tên đăng nhập hoặc mật khẩu không đúng.");
            context.Session.Clear(); context.Session.SetString(UserKey, user.Id.ToString());
            return Results.Ok(user);
        }).RequireRateLimiting("internal-login");
        api.MapGet("/auth/me", async (HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await User(c, r, ct)));
        api.MapPost("/auth/logout", async (HttpContext c, IAntiforgery csrf) => { await csrf.ValidateRequestAsync(c); c.Session.Clear(); return Results.NoContent(); });
        api.MapPost("/auth/change-password", async (EmployeeChangePasswordInput input, HttpContext c, IAntiforgery csrf, IInternalRepository r, IEmployeeAdmin accounts, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var user = await User(c, r, ct);
            await accounts.ChangePassword(user.Id, input, ct);
            return Results.Ok(await r.GetUser(user.Id, ct));
        });
        // "Hồ sơ của tôi": the employee's own account. A new email is confirmed by a code sent to it.
        api.MapGet("/profile", async (HttpContext c, IInternalRepository r, EmployeeProfileService profile, CancellationToken ct) =>
            Results.Ok(await profile.GetAsync((await User(c, r, ct)).Id, ct)));
        api.MapPost("/profile/email/code", async (EmailCodeInput input, HttpContext c, IAntiforgery csrf, IInternalRepository r, EmployeeProfileService profile, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var user = await User(c, r, ct); RequirePasswordChanged(user);
            return Results.Ok(new { sentTo = await profile.SendEmailCodeAsync(user.Id, input.Password, input.NewEmail, ct) });
        });
        api.MapPost("/profile/email", async (EmailConfirmInput input, HttpContext c, IAntiforgery csrf, IInternalRepository r, EmployeeProfileService profile, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var user = await User(c, r, ct); RequirePasswordChanged(user);
            return Results.Ok(await profile.ConfirmEmailAsync(user.Id, input.Code, ct));
        });
        var admin = api.MapGroup("/admin").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var user = await User(c, c.RequestServices.GetRequiredService<IInternalRepository>(), c.RequestAborted);
            RequirePasswordChanged(user);
            if (!user.CanManageEmployees) throw new InternalAppException(403, "Bạn không có quyền quản lý tài khoản nhân viên.");
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        admin.MapGet("/employees", async (IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.Employees(ct)));
        admin.MapGet("/departments", async (IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.Departments(ct)));
        admin.MapPost("/employees", async (CreateEmployeeInput input, HttpContext c, IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.Create(input, Current(c), ct)));
        admin.MapPut("/employees/{id:long}/position", async (long id, EmployeePositionInput input, HttpContext c, IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.SetPosition(id, input, Current(c), ct)));
        admin.MapPut("/employees/{id:long}/status", async (long id, EmployeeStatusInput input, HttpContext c, IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.SetStatus(id, input, Current(c), ct)));
        admin.MapPost("/employees/{id:long}/reset-password", async (long id, HttpContext c, IEmployeeAdmin a, CancellationToken ct) => Results.Ok(await a.ResetPassword(id, Current(c), ct)));
        // Screen groups: any one permission opens the group; writes inside may require more (checked per endpoint).
        RouteGroupBuilder Screen(string prefix, string denied, params string[] permissions) => api.MapGroup(prefix).AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var user = await User(c, c.RequestServices.GetRequiredService<IInternalRepository>(), c.RequestAborted);
            RequirePasswordChanged(user);
            if (!permissions.Any(user.Permissions.Contains) && !user.IsManager && user.Username != "sysadmin") throw new InternalAppException(403, denied);
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        // Sale (customer.manage) sees and manages the customers they look after; Hành chính and Ban Giám Đốc
        // (customer.manage.all) every customer, and they choose who looks after it (016-customer-owner.sql).
        var crm = Screen("/crm", "Bạn không có quyền quản lý khách hàng.", CatalogRules.CustomerManage, CatalogRules.CustomerManageAll);
        crm.MapGet("/customers", async (string? search, HttpContext c, ICatalogAdmin a, CancellationToken ct) =>
            Results.Ok(await a.Customers(search, AllCustomers(Current(c)) ? null : Current(c).Id, ct)));
        crm.MapGet("/owners", async (HttpContext c, MasterDbContext db, CancellationToken ct) =>
        {
            if (!AllCustomers(Current(c))) throw new InternalAppException(403, "Chỉ Phòng Hành Chính - Pháp Chế và Ban Giám Đốc chuyển được khách hàng.");
            return Results.Ok(await CustomerOwners(db, ct));
        });
        crm.MapPut("/customers/{id:long}/owner", async (long id, CustomerOwnerInput input, HttpContext c, MasterDbContext db, ICatalogAdmin a, CancellationToken ct) =>
        {
            var user = Current(c);
            if (!AllCustomers(user)) throw new InternalAppException(403, "Chỉ Phòng Hành Chính - Pháp Chế và Ban Giám Đốc chuyển được khách hàng.");
            var customer = await db.Customers.SingleOrDefaultAsync(x => x.CustomerId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");
            if (input.OwnerId is { } owner && !(await CustomerOwners(db, ct)).Any(o => o.Id == owner))
                throw new ArgumentException("Người phụ trách phải là nhân viên Kinh doanh đang hoạt động.");
            if (customer.CustomerOwnerEmployeeId != input.OwnerId)
            {
                var now = DateTime.UtcNow;
                customer.CustomerOwnerEmployeeId = input.OwnerId; customer.UpdatedAt = now; customer.UpdatedBy = user.Id;
                db.AuditLogs.Add(new ContractManagement.Domain.Entities.Master.AuditLog { ActionType = "TRANSFER_OWNER", EntityName = "CUSTOMER", EntityId = id.ToString(), ActorType = "EMPLOYEE", ActorId = user.Id,
                    NewData = System.Text.Json.JsonSerializer.Serialize(new { ownerId = input.OwnerId }), CreatedAt = now });
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(await a.CustomerRow(id, ct));
        });
        // A new customer with an email is told their portal account exists and how to set a password.
        crm.MapPost("/customers", async (CustomerInput input, HttpContext c, IInternalRepository r, ICatalogAdmin a, ContractManagement.Infrastructure.Email.CustomerWelcomeMailer welcome, CancellationToken ct) =>
        {
            var created = await r.CreateCustomer(input, Current(c).Id, ct);
            if (!string.IsNullOrWhiteSpace(input.Email)) await welcome.TrySendAsync(created.Id, ct);
            return Results.Ok(await a.CustomerRow(created.Id, ct));
        });
        crm.MapPost("/customers/{id:long}/invite", async (long id, HttpContext c, MasterDbContext db, ContractManagement.Infrastructure.Email.CustomerWelcomeMailer welcome, CancellationToken ct) =>
        {
            await RequireCustomer(db, Current(c), id, AllCustomers(Current(c)), ct);
            return Results.Ok(new { sentTo = await welcome.SendAsync(id, ct) });
        });
        crm.MapPost("/customers/{id:long}/credentials", async (long id, CustomerCredentialsInput input, HttpContext c, MasterDbContext db, ContractManagement.Infrastructure.Email.CustomerWelcomeMailer welcome, CancellationToken ct) =>
        {
            var user = Current(c);
            if (!AllCustomers(user) && !user.CanManageAll && !user.CanCheck)
                throw new InternalAppException(403, "Chỉ phòng Hành chính - Pháp chế và Ban Giám Đốc mới có quyền gửi thông tin tài khoản cho khách hàng.");
            await RequireCustomer(db, user, id, true, ct);
            return Results.Ok(new { sentTo = await welcome.SendCredentialsAsync(id, input.Password ?? "", input.Phone, user.Id, ct) });
        });
        crm.MapPut("/customers/{id:long}", async (long id, CustomerUpdateInput input, HttpContext c, MasterDbContext db, ICatalogAdmin a, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, AllCustomers(Current(c)), ct); return Results.Ok(await a.UpdateCustomer(id, input, Current(c), ct)); });
        crm.MapPut("/customers/{id:long}/status", async (long id, ActiveInput input, HttpContext c, MasterDbContext db, ICatalogAdmin a, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, AllCustomers(Current(c)), ct); return Results.Ok(await a.SetCustomerStatus(id, input, Current(c), ct)); });
        crm.MapGet("/customers/{id:long}/businesses", async (long id, HttpContext c, MasterDbContext db, IInternalRepository r, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, AllCustomers(Current(c)), ct); return Results.Ok(await r.Businesses(id, ct)); });
        crm.MapPost("/customers/{id:long}/businesses", async (long id, BusinessInput input, HttpContext c, MasterDbContext db, IInternalRepository r, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, AllCustomers(Current(c)), ct); return Results.Ok(await r.CreateBusiness(id, input, Current(c).Id, ct)); });
        var catalog = Screen("/catalog", "Bạn không có quyền xem sản phẩm và dịch vụ.", CatalogRules.ProductRead, CatalogRules.ProductManage);
        catalog.MapGet("/products", async (string? search, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Products(search, ct)));
        catalog.MapGet("/categories", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Categories(ct)));
        catalog.MapGet("/services", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Services(ct)));
        catalog.MapPost("/products", async (ProductInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.SaveProduct(null, input, RequireProductManage(c), ct)));
        catalog.MapPut("/products/{id:long}", async (long id, ProductInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.SaveProduct(id, input, RequireProductManage(c), ct)));
        var org = Screen("/org", "Bạn không có quyền quản lý phòng ban.", CatalogRules.DepartmentManage);
        org.MapGet("/departments", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Departments(ct)));
        org.MapPut("/departments/{id:long}", async (long id, DepartmentInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.UpdateDepartment(id, input, Current(c), ct)));
        // Hỗ trợ khách hàng: Chỉ nhân sự thuộc 3 phòng ban (Kỹ thuật, Kế toán, Hành chính) có quyền ticket.handle/ticket.manage.all mới được truy cập
        var tickets = api.MapGroup("/tickets").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var user = await User(c, c.RequestServices.GetRequiredService<IInternalRepository>(), c.RequestAborted);
            RequirePasswordChanged(user);
            if (!user.Permissions.Contains("ticket.handle") && !user.Permissions.Contains("ticket.manage.all"))
                throw new InternalAppException(403, "Bạn không có quyền xử lý yêu cầu hỗ trợ.");
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        tickets.MapGet("", async (string? status, string? priority, string? department, string? keyword, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(await s.GetAllForAdminAsync(TicketActor(c).Id, status, priority, department, keyword, ct)));
        tickets.MapGet("/{id:int}", async (int id, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            await s.GetByIdAsync(id, TicketActor(c).Id, ct) is { } t ? Results.Ok(t) : throw new InternalAppException(404, "Không tìm thấy yêu cầu hỗ trợ hoặc bạn không có quyền truy cập."));
        tickets.MapGet("/{id:int}/staff", async (int id, HttpContext c, ISupportTicketService s, CancellationToken ct) => Results.Ok(await s.GetDepartmentStaffAsync(id, TicketActor(c).Id, ct)));
        tickets.MapPost("/{id:int}/assign", async (int id, AssignTicketToStaffRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Found(await s.AssignToStaffAsync(id, TicketManager(c).Id, TicketActor(c).Name, input, ct)));
        tickets.MapPost("/{id:int}/staff-complete", async (int id, StaffCompleteTicketRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Resolution)) throw new ArgumentException("Vui lòng nhập phương án/kết quả xử lý.");
            return Found(await s.StaffCompleteAsync(id, TicketActor(c).Id, TicketActor(c).Name, input, ct));
        });
        tickets.MapPost("/{id:int}/resolve", async (int id, HeadResolveTicketRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Found(await s.HeadResolveAsync(id, TicketManager(c).Id, TicketActor(c).Name, input, ct)));
        tickets.MapPatch("/{id:int}/status", async (int id, UpdateTicketStatusRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Found(await s.UpdateTicketStatusAsync(id, TicketManager(c).Id, TicketActor(c).Name, input, ct)));
        // Ticket notifications of main's AdminNotificationsController: the service already scopes them to the head / assigned staff.
        tickets.MapGet("/notifications", async (string? status, int? limit, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            var (items, unreadCount) = await s.GetAdminNotificationsAsync(TicketActor(c).Id, status ?? "all", Math.Clamp(limit ?? 20, 1, 50), ct);
            return Results.Ok(new { items, unreadCount });
        });
        tickets.MapGet("/notifications/unread-count", async (HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(new { count = await s.GetAdminUnreadNotificationCountAsync(TicketActor(c).Id, ct) }));
        tickets.MapPatch("/notifications/{id:int}/read", async (int id, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            await s.MarkAdminNotificationReadAsync(TicketActor(c).Id, id, ct) ? Results.NoContent() : throw new InternalAppException(404, "Không tìm thấy thông báo."));
        tickets.MapPost("/notifications/mark-all-read", async (HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(new { changed = await s.MarkAllAdminNotificationsReadAsync(TicketActor(c).Id, ct) }));
        var secured = api.MapGroup("").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var user = await User(c, c.RequestServices.GetRequiredService<IInternalRepository>(), c.RequestAborted);
            RequirePasswordChanged(user);
            var path = (c.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
            var canSign = user.CanApprove && c.GetEndpoint()?.Metadata.GetMetadata<CompanySigningAction>() is not null;
            var legalContractRead = user.CanManageLegal && HttpMethods.IsGet(c.Request.Method)
                && (path is "/api/internal/contract-drafts" or "/api/internal/contract-progress" or "/api/internal/contract-archive"
                    or "/api/internal/contract-signing-monitor" or "/api/internal/company-signing/queue" or "/api/internal/contract-templates"
                    or "/api/internal/contract-template-library" or "/api/internal/document-categories"
                    || System.Text.RegularExpressions.Regex.IsMatch(path,
                        @"^/api/internal/(contract-drafts/\d+(/(approval|internal-approval|internal-comments|checklist|reviews|versions|word/content|print-pdf|related))?|contract-signing-monitor/\d+|company-signing/\d+/document|contract-templates/\d+(/(checklist|format))?)$"));
            var legalWorkflowAction = user.CanManageLegal && HttpMethods.IsPost(c.Request.Method)
                && System.Text.RegularExpressions.Regex.IsMatch(path,
                    @"^/api/internal/(contract-drafts/\d+/(approval/(approve|return)|internal-approval|internal-comments|checklist|start-signing)|contract-comments/\d+/resolve)$");
            if (!user.CanWrite && !user.CanCheck && !user.CanApprove && !user.CanManageTemplates && !legalContractRead && !legalWorkflowAction)
                throw new InternalAppException(403, "Bạn không có quyền sử dụng chức năng hợp đồng.");
            if (!HttpMethods.IsGet(c.Request.Method) && !user.CanWrite && !user.CanCheck && !user.CanManageTemplates
                && !legalWorkflowAction && !canSign && !path.Contains("/approval/") && !path.EndsWith("/internal-approval") && !path.EndsWith("/internal-comments") && !path.EndsWith("/word-preview"))
                throw new InternalAppException(403, "Bạn chỉ có quyền xem và duyệt hồ sơ.");
            if (path.StartsWith("/api/internal/contract-templates/") && (path.EndsWith("/extract") || path.EndsWith("/preview") || path.EndsWith("/activate") || path.EndsWith("/import")) && !user.CanManageTemplates)
                throw new InternalAppException(403, "Chỉ Hành chính được quản lý mẫu hợp đồng.");
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        secured.MapGet("/contract-drafts/{id:long}/approval", async (long id, HttpContext c, InternalApprovalService s, CancellationToken ct) => Results.Ok(await s.View(id, Current(c), ct)));
        secured.MapPost("/contract-drafts/{id:long}/approval/{action}", async (long id, string action, ApprovalAction input, HttpContext c, InternalApprovalService s, CancellationToken ct) => { await s.Act(id, action, input, Current(c), ct); return Results.NoContent(); });
        secured.MapPost("/contract-drafts/{id:long}/internal-comments", async (long id, InternalCommentInput input, HttpContext c, InternalApprovalService s, CancellationToken ct) => { await s.Comment(id, input, Current(c), ct); return Results.NoContent(); });
        // Any signed-in employee: the board sees every contract ending within 30 days, others the ones they are in charge of.
        api.MapGet("/contracts/expiring", async (HttpContext c, IInternalRepository r, ContractExpiryReminders reminders, CancellationToken ct) =>
        {
            var user = await User(c, r, ct);
            RequirePasswordChanged(user);
            return Results.Ok(await reminders.Expiring(ContractExpiryReminders.Today(DateTime.UtcNow), user.CanManageAll ? null : user.Id, ct));
        });
        secured.MapPatch("/contract-templates/{id:long}/name", async (long id, TemplateNameInput input, HttpContext c, InternalRepository repository, CancellationToken ct) =>
            Results.Ok(new { name = await repository.RenameTemplate(id, input.Name, Current(c), ct) }));
        secured.MapDelete("/contract-templates/{id:long}", async (long id, HttpContext c, InternalRepository repository, CancellationToken ct) =>
        { await repository.DeleteTemplate(id, Current(c), ct); return Results.NoContent(); });
        // The template library (Tạo mẫu văn bản): every type, hidden ones included; changing a template needs template.manage.
        secured.MapGet("/contract-template-library", async (InternalRepository repository, CancellationToken ct) => Results.Ok(await repository.TemplateLibrary(ct)));
        secured.MapPut("/contract-templates/{id:long}", async (long id, TemplateInfoInput input, HttpContext c, InternalRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.UpdateTemplate(id, input, Current(c), ct)));
        secured.MapPost("/contract-templates/{id:long}/restore", async (long id, HttpContext c, InternalRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.RestoreTemplate(id, Current(c), ct)));
        // Contract print format (Thiết lập tham số hợp đồng): read by everyone, changed by Hành chính.
        secured.MapGet("/contract-templates/{id:long}/format", async (long id, ContractFormatSettings s, CancellationToken ct) => Results.Ok(await s.Get(id, ct)));
        secured.MapPost("/contract-templates/{id:long}/format-preview", async (long id, ContractManagement.Domain.Internal.ContractFormat input, HttpContext c, IInternalRepository repository, InternalWordTemplates word, CancellationToken ct) =>
        {
            if (!Current(c).CanManageTemplates) throw new InternalAppException(403, "Bạn không có quyền quản lý mẫu.");
            var template = (await repository.Templates(ct)).SingleOrDefault(t => t.Id == id && t.WordStorageKey is not null)
                ?? throw new InternalAppException(404, "Không tìm thấy mẫu Word đang hoạt động.");
            var pdf = await word.PreviewStoredWord(template with { PrintFormat = ContractFormatSettings.ValidateWord(input) }, true, ct);
            return Results.Ok(new { base64 = Convert.ToBase64String(pdf) });
        });
        secured.MapPut("/contract-templates/{id:long}/format", async (long id, ContractManagement.Domain.Internal.ContractFormat input, HttpContext c, ContractFormatSettings s, CancellationToken ct) => Results.Ok(await s.Save(id, input, Current(c), ct)));
        // Document types (Loại văn bản): everyone reads them for labels; Hành chính adds, changes, hides or deletes them.
        secured.MapGet("/document-categories", async (DocumentCategories s, CancellationToken ct) => Results.Ok(await s.List(ct)));
        secured.MapPost("/document-categories", async (DocumentCategoryInput input, HttpContext c, DocumentCategories s, CancellationToken ct) =>
            Results.Ok(await s.Create(input, Current(c), ct)));
        secured.MapPut("/document-categories/{id:int}", async (int id, DocumentCategoryInput input, HttpContext c, DocumentCategories s, CancellationToken ct) =>
            Results.Ok(await s.Update(id, input, Current(c), ct)));
        secured.MapDelete("/document-categories/{id:int}", async (int id, HttpContext c, DocumentCategories s, CancellationToken ct) =>
        { await s.Delete(id, Current(c), ct); return Results.NoContent(); });
        // The contract bell: every signed-in employee sees their own contract notifications; the item opens the contract.
        api.MapGet("/contract-notifications", async (int? limit, HttpContext c, IInternalRepository r, ContractNotificationFeed feed, CancellationToken ct) =>
            Results.Ok(await feed.List(await User(c, r, ct), limit ?? 20, ct)));
        api.MapPost("/contract-notifications/{id:long}/read", async (long id, HttpContext c, IInternalRepository r, ContractNotificationFeed feed, CancellationToken ct) =>
        { await feed.MarkRead(await User(c, r, ct), id, ct); return Results.NoContent(); });
        api.MapPost("/contract-notifications/read-all", async (HttpContext c, IInternalRepository r, ContractNotificationFeed feed, CancellationToken ct) =>
        { await feed.MarkRead(await User(c, r, ct), null, ct); return Results.NoContent(); });
        secured.MapGet("/contract-templates", async (IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Templates(ct)));
        // A rejected or cancelled contract removed for good (Quản lý hợp đồng).
        secured.MapDelete("/contract-drafts/{id:long}", async (long id, HttpContext c, ContractHardDelete service, CancellationToken ct) =>
        { await service.Delete(id, Current(c), ct); return Results.NoContent(); });
        secured.MapGet("/contract-progress", async ([AsParameters] ProgressFilter filter, HttpContext c, ContractProgressService service, CancellationToken ct) => Results.Ok(await service.List(filter, Current(c), ct)));
        secured.MapGet("/contract-drafts/{id:long}/word/content", async (long id, HttpContext c, InternalDraftService drafts, InternalWordTemplates service, CancellationToken ct) => {
            var draft = await drafts.Get(id, Current(c), ct);
            var bytes = await service.ReadWord(draft.Content.TemplateSnapshot!, ct);
            return Results.Ok(new { name = draft.Content.Name, blocks = InternalWordDocument.Blocks(bytes, true), sourceToken = "", documentBase64 = "" });
        });
        var wordDrafts = secured.MapGroup("/contract-drafts/{id:long}/word").AddEndpointFilter(async (invocation, next) => {
            var context = invocation.HttpContext;
            var actor = Current(context);
            var id = invocation.GetArgument<long>(0);
            var drafts = context.RequestServices.GetRequiredService<InternalDraftService>();
            var draft = await drafts.Get(id, actor, context.RequestAborted);
            if (!actor.CanEditDraftTerms(draft.EmployeeId)) throw new InternalAppException(403, "Bạn không có quyền sửa Word của nháp này.");
            if (!draft.CanEdit) throw new InternalAppException(409, "Hồ sơ đang khóa.");
            context.Items["draftWordSource"] = draft.Content.TemplateSnapshot;
            return await next(invocation);
        });
        var newDraftWord = secured.MapGroup("/contract-drafts/from-template/{id:long}/word").AddEndpointFilter(async (invocation, next) => {
            var context = invocation.HttpContext;
            var actor = Current(context);
            if (!actor.CanWrite && !actor.CanCheck) throw new InternalAppException(403, "Bạn không có quyền tạo nháp.");
            var templates = await context.RequestServices.GetRequiredService<IInternalRepository>().Templates(context.RequestAborted);
            context.Items["draftWordSource"] = templates.SingleOrDefault(t => t.Id == invocation.GetArgument<long>(0) && t.WordStorageKey != null)
                ?? throw new InternalAppException(404, "Không tìm thấy mẫu Word đang hoạt động.");
            return await next(invocation);
        });
        newDraftWord.MapPost("/extract", async (long id, HttpContext c, InternalWordTemplates service, CancellationToken ct) => {
            var bytes = await service.ReadWord((ContractManagement.Domain.Internal.TemplateDefinition)c.Items["draftWordSource"]!, ct);
            return Results.Ok(await service.Extract(new TemplateFileInput("hop-dong.docx", Convert.ToBase64String(bytes)), Current(c).Id, ct));
        });
        newDraftWord.MapPost("/editor-preview", async (long id, WordEditorPreview input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.EditorPreview(input, (ContractManagement.Domain.Internal.TemplateDefinition)c.Items["draftWordSource"]!, null, Current(c).Id, ct)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        wordDrafts.MapPost("/extract", async (long id, HttpContext c, InternalDraftService drafts, InternalWordTemplates service, CancellationToken ct) => {
            var draft = await drafts.Get(id, Current(c), ct);
            if (!draft.CanEdit) throw new InternalAppException(409, "Hồ sơ đang khóa.");
            var bytes = await service.ReadWord(draft.Content.TemplateSnapshot!, ct);
            return Results.Ok(await service.Extract(new TemplateFileInput("hop-dong.docx", Convert.ToBase64String(bytes)), Current(c).Id, ct));
        });
        wordDrafts.MapPost("/editor-preview", async (long id, WordEditorPreview input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.EditorPreview(input, (ContractManagement.Domain.Internal.TemplateDefinition)c.Items["draftWordSource"]!, id, Current(c).Id, ct)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        wordDrafts.MapPost("/preview", async (long id, WordTemplateInput input, HttpContext c, InternalWordTemplates service, CancellationToken ct) => Results.Ok(await service.Preview(input, Current(c).Id, ct)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        wordDrafts.MapPost("/activate", async (long id, WordDraftSave input, HttpContext c, InternalWordTemplates service, CancellationToken ct) => { await service.SaveDraftWord(id, input, Current(c), ct); return Results.NoContent(); })
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        var management = secured.MapGroup("/template-library").AddEndpointFilter(async (ctx, next) =>
        {
            if (!Current(ctx.HttpContext).CanManageTemplates) return Results.StatusCode(403);
            return await next(ctx);
        });
        management.MapGet("", async (InternalRepository r, CancellationToken ct) => Results.Ok(await r.TemplateLibrary(true, ct)));
        management.MapGet("/{id:long}/editor", async (long id, InternalRepository r, MasterDbContext db, InternalWordTemplates service, HttpContext c, CancellationToken ct) =>
        {
            var row = await db.ContractTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.ContractTemplateId == id && t.IsActive, ct);
            var template = (await r.TemplateLibrary(true, ct, id)).SingleOrDefault();
            if (row == null || template == null) return Results.NotFound();
            return Results.Ok(new { extraction = await service.OpenTemplate(template, Current(c).Id, ct),
                stamp = (row.UpdatedDate ?? row.CreatedDate).Ticks.ToString(), template });
        });
        management.MapPost("/{id:long}/preview", async (long id, WordTemplateInput input, HttpContext c, InternalRepository repository, InternalWordTemplates service, CancellationToken ct) =>
        {
            var template = (await repository.TemplateLibrary(true, ct, id)).SingleOrDefault()
                ?? throw new InternalAppException(404, "Không tìm thấy mẫu đang hoạt động.");
            var format = template.WordStorageKey is null ? null : template.PrintFormat;
            return Results.Ok(await service.Preview(input, Current(c).Id, ct, format));
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        management.MapPut("/{id:long}", async (long id, string stamp, WordActivation input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.Activate(input, Current(c).Id, ct, id, stamp))).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        secured.MapPost("/contract-templates/word/extract", async (TemplateFileInput input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.Extract(input, Current(c).Id, ct)));
        secured.MapPost("/contract-templates/word/preview", async (WordTemplateInput input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.Preview(input, Current(c).Id, ct))).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        secured.MapPost("/contract-templates/word/activate", async (WordActivation input, HttpContext c, InternalWordTemplates service, CancellationToken ct) =>
            Results.Ok(await service.Activate(input, Current(c).Id, ct))).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(48 * 1024 * 1024));
        secured.MapPost("/contract-drafts/word-preview", async (WordDraftPreview input, HttpContext c,
            InternalDraftService drafts, IInternalRepository repository, InternalWordTemplates service, CancellationToken ct) =>
        {
            var draft = input.DraftId.HasValue ? await drafts.Get(input.DraftId.Value, Current(c), ct) : null;
            var template = draft is not null
                ? draft.Content.TemplateSnapshot
                : (await repository.Templates(ct)).SingleOrDefault(t => t.Id == input.Content.TemplateId);
            if (template is null) throw new ArgumentException("Không tìm thấy mẫu hợp đồng.");
            var previewContent = draft?.Content ?? input.Content;
            if (draft is null || draft.CanEdit) previewContent.UseTimesNewRoman = true;
            var bytes = await service.Render(previewContent, template, input.Pdf, ct);
            return Results.Ok(new { base64 = Convert.ToBase64String(bytes), contentType = input.Pdf ? "application/pdf" : InternalWordDocument.Mime });
        });
        secured.MapGet("/contract-templates/{id:long}/word-details", async (long id, IInternalRepository repository, InternalWordTemplates service, CancellationToken ct) => {
            var template = (await repository.Templates(ct)).SingleOrDefault(t => t.Id == id)
                ?? throw new ArgumentException("Không tìm thấy mẫu hợp đồng đang hoạt động.");
            return Results.Ok(InternalWordDetails.Extract(await service.ReadWord(template, ct), template.Name));
        });
        secured.MapPost("/contract-templates/{id:long}/word-preview", async (long id, WordStoredPreview input, IInternalRepository repository, InternalWordTemplates service, CancellationToken ct) => {
            var template = (await repository.Templates(ct)).SingleOrDefault(t => t.Id == id)
                ?? throw new ArgumentException("Không tìm thấy mẫu hợp đồng đang hoạt động.");
            var bytes = await service.PreviewStoredWord(template, input.Pdf, ct);
            return Results.Ok(new { base64 = Convert.ToBase64String(bytes), contentType = input.Pdf ? "application/pdf" : InternalWordDocument.Mime });
        });
        secured.MapPost("/contract-templates/extract", (TemplateFileInput input, IInternalTemplateImport importer) => Results.Ok(importer.Extract(input)));
        secured.MapPost("/contract-templates/import", async (TemplateImportInput input, HttpContext c, IInternalTemplateImport importer, CancellationToken ct) =>
            Results.Ok(await importer.Save(input, Current(c).Id, ct)));
        secured.MapGet("/contract-templates/{id:long}", async (long id, IInternalRepository r, CancellationToken ct) =>
            (await r.Templates(ct)).FirstOrDefault(t => t.Id == id) is { } t ? Results.Ok(t) : Results.NotFound());
        // Bên A of a draft refilled from the customer record (the person, or the legal entity chosen in the draft).
        secured.MapGet("/customers/{id:long}/profile", async (long id, long? businessId, InternalRepository repository, CancellationToken ct) =>
            await repository.CustomerProfileFor(id, businessId, ct) is { } profile ? Results.Ok(profile) : Results.NotFound());
        secured.MapGet("/number-rules/{owner:long}", async (long owner, long? customerId, HttpContext c, ContractNumberRules rules, CancellationToken ct) => {
            if (!Current(c).CanWrite && !Current(c).CanCheck) throw new InternalAppException(403, "Bạn không có quyền xem quy tắc.");
            await rules.CheckOwner(owner, customerId, ct);
            return Results.Ok(new { rule = await rules.Read(owner, ct) });
        });
        secured.MapPost("/number-rules/{owner:long}", async (long owner, long? customerId, NumberRule input, HttpContext c, ContractNumberRules rules, CancellationToken ct) =>
            Results.Ok(await rules.Save(owner, customerId, input, Current(c), false, ct)));
        secured.MapPut("/number-rules/{owner:long}", async (long owner, long? customerId, NumberRule input, HttpContext c, ContractNumberRules rules, CancellationToken ct) =>
            Results.Ok(await rules.Save(owner, customerId, input, Current(c), true, ct)));
        secured.MapGet("/customers/lookup", async (string? search, InternalRepository repository, CancellationToken ct) =>
            Results.Ok(await repository.LookupCustomers(search, ct)));
        // Drafts: a sale picks among their own customers (a sales manager too); Hành chính and Ban Giám Đốc among all.
        // A manager still opens the businesses of a customer in a draft of their team (DraftSeesAllCustomers).
        secured.MapGet("/customers", async (string? search, HttpContext c, IInternalRepository r, CancellationToken ct) =>
            Results.Ok(await r.Customers(search, ct, AllCustomers(Current(c)) ? null : Current(c).Id)));
        secured.MapGet("/customers/{id:long}/businesses", async (long id, HttpContext c, MasterDbContext db, IInternalRepository r, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, DraftSeesAllCustomers(Current(c)), ct); return Results.Ok(await r.Businesses(id, ct)); });
        secured.MapPost("/customers/{id:long}/businesses", async (long id, BusinessInput input, HttpContext c, MasterDbContext db, IInternalRepository r, CancellationToken ct) =>
        { await RequireCustomer(db, Current(c), id, DraftSeesAllCustomers(Current(c)), ct); return Results.Ok(await r.CreateBusiness(id, input, Current(c).Id, ct)); });
        secured.MapPost("/customers", async (CustomerInput input, HttpContext c, IInternalRepository r, ContractManagement.Infrastructure.Email.CustomerWelcomeMailer welcome, CancellationToken ct) =>
        {
            var created = await r.CreateCustomer(input, Current(c).Id, ct);
            if (!string.IsNullOrWhiteSpace(input.Email)) await welcome.TrySendAsync(created.Id, ct);
            return Results.Ok(created);
        });
        secured.MapGet("/products", async (string? search, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Products(search, ct)));
        secured.MapGet("/employees", async (HttpContext c, IInternalRepository r, CancellationToken ct) => {
            if (!Current(c).CanManageAll) throw new InternalAppException(403, "Bạn không có quyền xem danh sách nhân viên.");
            return Results.Ok(await r.Employees(ct));
        });
        secured.MapGet("/contract-drafts", async (string? search, string? ownerId, int? status, string? partyType, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            var user = Current(c); long? owner = user.CanManageContractWorkflow ? null : user.Id;
            if (!string.IsNullOrEmpty(ownerId)) { if (!user.CanManageContractWorkflow) throw new InternalAppException(403, "Không được lọc nháp của nhân viên khác.");
                if (!long.TryParse(ownerId, out var parsed) || parsed <= 0) throw new ArgumentException("Nhân viên không hợp lệ."); owner = parsed; }
            return Results.Ok(await r.Drafts(owner, search, status, ct, partyType));
        });
        secured.MapGet("/signed-contracts", async (string? search, int? page, long? documentId, string? sort, bool? desc, string? method, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.SignedContracts(Current(c), search, page ?? 1, ct, documentId, sort, desc ?? true, method is "PAPER" or "ELECTRONIC" ? method : null)));
        var signedPayments = secured.MapGroup("/signed-contracts/{documentId:long}/versions/{versionId:long}/payments");
        signedPayments.MapGet("", async (long documentId, long versionId, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.SignedPayments(Current(c), documentId, versionId, ct)));
        signedPayments.MapPost("/{installmentId:long}/confirm", async (long documentId, long versionId, long installmentId,
            HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
        {
            await signing.ConfirmSignedPayment(Current(c), documentId, versionId, installmentId, ct);
            return Results.NoContent();
        });
        secured.MapGet("/signed-contracts/{documentId:long}/versions/{versionId:long}/pdf", async (long documentId, long versionId,
            HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
        {
            c.Response.Headers.CacheControl = "private, no-store";
            return Results.File(await signing.InternalFinalDocument(Current(c), documentId, versionId, ct), "application/pdf");
        });
        secured.MapGet("/contract-archive", async (string? search, int? status, string? partyType, DateTime? createdFrom, DateTime? createdTo, int? limit, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            if (!Current(c).CanManageContractWorkflow) throw new InternalAppException(403, "Bạn không có quyền xem kho hợp đồng.");
            return Results.Ok(await r.ContractArchive(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), status, ct, partyType, createdFrom, createdTo, limit ?? 200));
        });
        secured.MapGet("/contract-signing-monitor", async (string? search, int? status, string? partyType, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            if (!Current(c).CanManageContractWorkflow) throw new InternalAppException(403, "Bạn không có quyền theo dõi ký.");
            return Results.Ok(await r.SigningMonitor(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), status, ct, partyType));
        });
        secured.MapGet("/contract-signing-monitor/{id:long}", async (long id, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            if (!Current(c).CanManageContractWorkflow) throw new InternalAppException(403, "Bạn không có quyền theo dõi ký.");
            return await r.SigningMonitorDetail(id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound();
        });
        secured.MapGet("/contract-drafts/{id:long}", async (long id, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Get(id, Current(c), ct)));
        // A contract's attached documents (appendix, handover record…) and the child templates it can make them from.
        secured.MapGet("/contract-drafts/{id:long}/related", async (long id, HttpContext c, InternalRepository r, CancellationToken ct) => Results.Ok(await r.Related(id, Current(c), ct)));
        secured.MapPost("/contract-drafts", async (SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(null, request, Current(c), ct)));
        secured.MapPut("/contract-drafts/{id:long}", async (long id, SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(id, request, Current(c), ct)));
        secured.MapPost("/contract-drafts/{id:long}/send-review", async (long id, SendReviewRequest input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.SendReview(id, input, Current(c), ct)));
        secured.MapGet("/contract-drafts/{id:long}/reviews", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) => Results.Ok(await r.Reviews(id, Current(c), ct)));
        secured.MapGet("/contract-drafts/{id:long}/versions", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) => Results.Ok(await r.VersionHistory(id, Current(c), ct)));
        secured.MapGet("/contract-drafts/{id:long}/internal-approval", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
        {
            var approval = await r.InternalApproval(id, Current(c), ct);
            return approval is null ? Results.Content("null", "application/json") : Results.Ok(approval);
        });
        secured.MapPost("/contract-drafts/{id:long}/internal-approval", async (long id, InternalApprovalDecisionRequest input, HttpContext c, IContractReviewRepository r, IInternalSigningService signing, CancellationToken ct) =>
        {
            var actor = Current(c);
            var state = await r.ResolveInternalApproval(id, input, actor, ct);
            if (!input.Approve) return Results.Ok(state);
            var signingSession = await signing.StartAfterFinalApproval(id, actor, ct);
            return Results.Ok(state with { SigningStarted = signingSession is not null });
        });
        secured.MapPost("/contract-drafts/{id:long}/ensure-signing", async (long id, HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.StartAfterFinalApproval(id, Current(c), ct)));
        secured.MapGet("/company-signing/queue", async (HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.Queue(Current(c), ct)));
        secured.MapPost("/contract-drafts/{id:long}/start-signing", async (long id, StartCompanySigningRequest input, HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.Start(id, input, Current(c), ct))).WithMetadata(new CompanySigningAction());
        secured.MapGet("/contract-drafts/{id:long}/print-pdf", async (long id, HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
        { var (pdf, name) = await signing.PrintPdf(id, Current(c), ct); return Results.File(pdf, "application/pdf", name); });
        secured.MapGet("/company-signing/{id:long}/document", async (long id, HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
            Results.File(await signing.CompanyDocument(id, Current(c), ct), "application/pdf", $"contract-{id}.pdf"));
        secured.MapPost("/company-signing/{id:long}/sign", async (long id, CompanySignatureRequest input, HttpContext c, IInternalSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.SignCompany(id, input, Current(c), ct))).WithMetadata(new CompanySigningAction());
        secured.MapPost("/contract-drafts/{id:long}/versions", async (long id, NewVersionRequest input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.CreateVersion(id, input, Current(c), ct)));
        secured.MapPost("/contract-reviews/{id:long}/followup", async (long id, FollowupRequest input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.Followup(id, input, Current(c), ct)));
        secured.MapPost("/contract-reviews/{id:long}/comments", async (long id, CommentInput input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.EmployeeComment(id, input, Current(c), ct)));
        secured.MapPost("/contract-comments/{id:long}/resolve", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
        { await r.ResolveComment(id, Current(c), ct); return Results.NoContent(); });
        secured.MapPost("/contract-reviews/{id:long}/resend-link", async (long id, ResendLinkRequest input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.ResendLink(id, input, Current(c), ct)));
        secured.MapPost("/customers/{id:long}/reset-temp-password", (long id, HttpContext c, IContractReviewRepository r, MasterDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, CancellationToken ct) =>
            IssueCustomerPassword(id, r.ResetTemporaryPassword(id, Current(c), ct), db, cache, ct));
        secured.MapPost("/customers/{id:long}/account", (long id, HttpContext c, IContractReviewRepository r, MasterDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, CancellationToken ct) =>
            IssueCustomerPassword(id, r.CreateCustomerAccount(id, Current(c), ct), db, cache, ct));
        static async Task<IResult> IssueCustomerPassword(long id, Task<ResetTemporaryPasswordResult> issue, MasterDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, CancellationToken ct)
        {
            var result = await issue;
            // A new password also lifts the wrong-password lock, so the customer can log in right away.
            if (ContractManagement.Domain.Internal.PhoneNumber.Normalize(await db.Customers.Where(x => x.CustomerId == id).Select(x => x.CustomerPersonalPhone).SingleOrDefaultAsync(ct)) is { } phone)
                cache.Remove(ReviewLinkOtp.LoginFailureKey(phone));
            return Results.Ok(result);
        }
        // The sale resends the login code: lifts the customer's code / phone / password locks and texts a new code to the contract phone.
        secured.MapPost("/contract-reviews/{id:long}/resend-otp", async (long id, HttpContext c, ICustomerPortalRepository r, ReviewLinkOtp otp, CancellationToken ct) =>
        { await otp.ResendByEmployee(await r.ActiveLink(id, Current(c), ct), ct); return Results.NoContent(); });
        ScanEndpoints.Map(api);
        CustomerPortalEndpoints.Map(app);
        await app.RunAsync();
    }
    private static InternalUser Current(HttpContext c) => (InternalUser)c.Items[UserKey]!;
    private static (int Id, string Name) TicketActor(HttpContext c) { var user = Current(c); return (CustomerPortalEndpoints.TicketId(user.Id), user.Name); }
    /// <summary>Assigning, closing, replying to the customer and changing status are the department head's (main's workflow; the service relies on the UI for assign).</summary>
    private static (int Id, string Name) TicketManager(HttpContext c) =>
        (Current(c).Permissions.Contains("ticket.manage.all") || Current(c).IsManager || Current(c).Username == "sysadmin")
            ? TicketActor(c) : throw new InternalAppException(403, "Chỉ trưởng phòng được phân công, trao đổi với khách hoặc đóng yêu cầu hỗ trợ.");
    private static IResult Found<T>(T? value) where T : class => value is null ? throw new InternalAppException(404, "Không tìm thấy yêu cầu hỗ trợ.") : Results.Ok(value);
    private static InternalUser RequireProductManage(HttpContext c) => Current(c) is var user && user.Permissions.Contains(CatalogRules.ProductManage)
        ? user : throw new InternalAppException(403, "Chỉ quản lý kinh doanh được thêm hoặc sửa sản phẩm.");
    private static void RequirePasswordChanged(InternalUser user)
    {
        if (user.MustChangePassword) throw new InternalAppException(403, "Bạn cần đổi mật khẩu tạm trước khi tiếp tục.", "PASSWORD_CHANGE_REQUIRED");
    }
    private static async Task<InternalUser> User(HttpContext context, IInternalRepository repository, CancellationToken ct)
    {
        if (!long.TryParse(context.Session.GetString(UserKey), out var id)) throw new InternalAppException(401, "Vui lòng đăng nhập.");
        return await repository.GetUser(id, ct) ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");
    }
    public sealed record TemplateNameInput(string? Name);
    public sealed record LoginInput(string Username, string Password);
    public sealed record CustomerOwnerInput(long? OwnerId);
    public sealed record CustomerOwnerOption(long Id, string Name);
    private static bool AllCustomers(InternalUser user) => user.Permissions.Contains(CatalogRules.CustomerManageAll);
    private static bool DraftSeesAllCustomers(InternalUser user) => AllCustomers(user) || user.CanManageAll;
    /// <summary>404 rather than 403, so a sale cannot learn that a customer of someone else exists.</summary>
    private static async Task RequireCustomer(MasterDbContext db, InternalUser user, long customerId, bool all, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(x => x.CustomerId == customerId && (all || x.CustomerOwnerEmployeeId == user.Id), ct))
            throw new InternalAppException(404, "Không tìm thấy khách hàng.");
    }
    /// <summary>Active employees whose role lets them create and look after customers (customer.manage).</summary>
    private static Task<List<CustomerOwnerOption>> CustomerOwners(MasterDbContext db, CancellationToken ct) =>
        (from e in db.Employees
         where e.EmployeeStatus == "ACTIVE" && (from er in db.EmployeeRoles
                 join rp in db.SystemRolePermissions on er.SystemRoleId equals rp.SystemRoleId
                 join p in db.SystemPermissions on rp.SystemPermissionId equals p.SystemPermissionId
                 where er.EmployeeId == e.EmployeeId && p.SystemPermissionCode == CatalogRules.CustomerManage select er).Any()
         orderby e.EmployeeDisplayName
         select new CustomerOwnerOption(e.EmployeeId, e.EmployeeDisplayName)).ToListAsync(ct);
    public sealed record EmailCodeInput(string? Password, string? NewEmail);
    public sealed record EmailConfirmInput(string? Code);
    public sealed record CustomerCredentialsInput(string? Password, string? Phone);
}

public sealed class StringLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String when long.TryParse(reader.GetString(), out var value) => value,
            _ => throw new JsonException("IDs must be numeric values or numeric strings.")
        };
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
