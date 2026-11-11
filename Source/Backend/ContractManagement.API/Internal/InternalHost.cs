using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Support;
using ContractManagement.Application.Features.Internal;
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
    public static async Task Run(WebApplicationBuilder builder, string[] args)
    {
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
        builder.Services.AddScoped<IInternalRepository, InternalRepository>();
        builder.Services.AddScoped<InternalDraftService>();
        builder.Services.AddScoped<IInternalTemplateImport, InternalTemplateImport>();
        builder.Services.AddScoped<IEmployeeAdmin, EmployeeAdminRepository>();
        builder.Services.AddScoped<ICatalogAdmin, CatalogRepository>();
        // Ticket workflow from main reads TICKET_* through the application context, here pointed at MASTER (no tenant in Internal mode).
        builder.Services.AddDbContext<ContractManagement.Infrastructure.Persistence.Application.DbDtctechContext>(o =>
            o.UseSqlServer(builder.Configuration.GetConnectionString("MasterDatabase"), sql => sql.EnableRetryOnFailure()));
        builder.Services.AddSingleton<ISupportTicketService, SupportTicketService>();
        builder.Services.AddScoped<IContractReviewRepository, ContractReviewRepository>();
        builder.Services.AddScoped<ICustomerPortalRepository, CustomerPortalRepository>();
        builder.Services.AddScoped<SmsCallbackProcessor>();
        builder.Services.Configure<EsmsOptions>(builder.Configuration.GetSection("Esms"));
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<SmsOutboxWriter>();
        if (builder.Configuration.GetValue<bool>("Esms:Enabled")) builder.Services.AddHttpClient<ISmsSender, EsmsSmsSender>(h => h.Timeout = TimeSpan.FromSeconds(30));
        else builder.Services.AddSingleton<ISmsSender, FakeSmsSender>();
        builder.Services.AddHostedService<SmsOutboxWorker>();
        builder.Services.AddOptions<SmtpEmailOptions>().Bind(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));
        builder.Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        builder.Services.AddScoped<ReviewLinkMailer>();
        builder.Services.AddScoped<ReviewLinkOtp>();
        builder.Services.AddMemoryCache();
        ScanEndpoints.Register(builder.Services, builder.Configuration);
        builder.Services.AddSingleton(new InternalTemplateStorage(builder.Configuration["InternalStorage:Root"]
            ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "private-storage", "internal-templates"))));
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
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
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
            catch (BadHttpRequestException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu gửi lên không hợp lệ." }); }
            catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Phiên bảo vệ đã hết hạn. Tải lại trang và thử lại." }); }
            catch (DbUpdateException e) { app.Logger.LogWarning(e, "Internal database write failed"); context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu trùng hoặc đã thay đổi. Kiểm tra rồi thử lại." }); }
            catch (Exception e) when (!context.RequestAborted.IsCancellationRequested) { app.Logger.LogError(e, "Internal request failed"); context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { message = "Không thể xử lý yêu cầu. Dữ liệu chưa được xác nhận lưu." }); }
        });
        if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        static bool IsCustomer(HttpContext c) => c.Request.Path.StartsWithSegments("/api/customer");
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
            if (!permissions.Any(user.Permissions.Contains)) throw new InternalAppException(403, denied);
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        var crm = Screen("/crm", "Bạn không có quyền quản lý khách hàng.", CatalogRules.CustomerManage);
        crm.MapGet("/customers", async (string? search, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Customers(search, ct)));
        crm.MapPost("/customers", async (CustomerInput input, HttpContext c, IInternalRepository r, ICatalogAdmin a, CancellationToken ct) =>
            Results.Ok(await a.CustomerRow((await r.CreateCustomer(input, Current(c).Id, ct)).Id, ct)));
        crm.MapPut("/customers/{id:long}", async (long id, CustomerUpdateInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.UpdateCustomer(id, input, Current(c), ct)));
        crm.MapPut("/customers/{id:long}/status", async (long id, ActiveInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.SetCustomerStatus(id, input, Current(c), ct)));
        crm.MapGet("/customers/{id:long}/businesses", async (long id, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Businesses(id, ct)));
        crm.MapPost("/customers/{id:long}/businesses", async (long id, BusinessInput input, HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.CreateBusiness(id, input, Current(c).Id, ct)));
        var catalog = Screen("/catalog", "Bạn không có quyền xem sản phẩm và dịch vụ.", CatalogRules.ProductRead, CatalogRules.ProductManage);
        catalog.MapGet("/products", async (string? search, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Products(search, ct)));
        catalog.MapGet("/categories", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Categories(ct)));
        catalog.MapGet("/services", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Services(ct)));
        catalog.MapPost("/products", async (ProductInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.SaveProduct(null, input, RequireProductManage(c), ct)));
        catalog.MapPut("/products/{id:long}", async (long id, ProductInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.SaveProduct(id, input, RequireProductManage(c), ct)));
        var org = Screen("/org", "Bạn không có quyền quản lý phòng ban.", CatalogRules.DepartmentManage);
        org.MapGet("/departments", async (ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.Departments(ct)));
        org.MapPut("/departments/{id:long}", async (long id, DepartmentInput input, HttpContext c, ICatalogAdmin a, CancellationToken ct) => Results.Ok(await a.UpdateDepartment(id, input, Current(c), ct)));
        // Hỗ trợ khách hàng: same calls as main's AdminSupportTicketsController, with the employee from the internal session.
        // The service scopes by department: the manager sees and assigns the department's tickets, staff see tickets assigned to them.
        var tickets = Screen("/tickets", "Bạn không có quyền xử lý yêu cầu hỗ trợ.", "ticket.handle", "ticket.manage.all");
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
        tickets.MapPost("/{id:int}/messages", async (int id, AdminReplyTicketRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.MessageContent)) throw new ArgumentException("Vui lòng nhập nội dung phản hồi.");
            return Found(await s.AddAdminMessageAsync(id, TicketManager(c).Id, TicketActor(c).Name, input, ct));
        });
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
            if (!user.CanWrite) throw new InternalAppException(403, "Bạn không có quyền sử dụng chức năng hợp đồng nháp.");
            c.Items[UserKey] = user;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        secured.MapGet("/contract-templates", async (IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Templates(ct)));
        secured.MapPost("/contract-templates/extract", (TemplateFileInput input, IInternalTemplateImport importer) => Results.Ok(importer.Extract(input)));
        secured.MapPost("/contract-templates/import", async (TemplateImportInput input, HttpContext c, IInternalTemplateImport importer, CancellationToken ct) =>
            Results.Ok(await importer.Save(input, Current(c).Id, ct)));
        secured.MapGet("/contract-templates/{id:long}", async (long id, IInternalRepository r, CancellationToken ct) =>
            (await r.Templates(ct)).FirstOrDefault(t => t.Id == id) is { } t ? Results.Ok(t) : Results.NotFound());
        secured.MapGet("/customers", async (string? search, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Customers(search, ct)));
        secured.MapGet("/customers/{id:long}/businesses", async (long id, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Businesses(id, ct)));
        secured.MapPost("/customers/{id:long}/businesses", async (long id, BusinessInput input, HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.CreateBusiness(id, input, Current(c).Id, ct)));
        secured.MapPost("/customers", async (CustomerInput input, HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.CreateCustomer(input, Current(c).Id, ct)));
        secured.MapGet("/products", async (string? search, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Products(search, ct)));
        secured.MapGet("/employees", async (HttpContext c, IInternalRepository r, CancellationToken ct) => {
            if (!Current(c).CanManageAll) throw new InternalAppException(403, "Bạn không có quyền xem danh sách nhân viên.");
            return Results.Ok(await r.Employees(ct));
        });
        secured.MapGet("/contract-drafts", async (string? search, string? ownerId, int? status, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            var user = Current(c); long? owner = user.CanManageAll ? null : user.Id;
            if (!string.IsNullOrEmpty(ownerId)) { if (!user.CanManageAll) throw new InternalAppException(403, "Không được lọc nháp của nhân viên khác.");
                if (!long.TryParse(ownerId, out var parsed) || parsed <= 0) throw new ArgumentException("Nhân viên không hợp lệ."); owner = parsed; }
            return Results.Ok(await r.Drafts(owner, search, status, ct));
        });
        secured.MapGet("/contract-drafts/{id:long}", async (long id, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Get(id, Current(c), ct)));
        secured.MapPost("/contract-drafts", async (SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(null, request, Current(c), ct)));
        secured.MapPut("/contract-drafts/{id:long}", async (long id, SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(id, request, Current(c), ct)));
        secured.MapPost("/contract-drafts/{id:long}/send-review", async (long id, SendReviewRequest input, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
            Results.Ok(await r.SendReview(id, input, Current(c), ct)));
        secured.MapGet("/contract-drafts/{id:long}/reviews", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) => Results.Ok(await r.Reviews(id, Current(c), ct)));
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
        secured.MapPost("/customers/{id:long}/reset-temp-password", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
        { await r.ResetTemporaryPassword(id, Current(c), ct); return Results.NoContent(); });
        ScanEndpoints.Map(api);
        CustomerPortalEndpoints.Map(app);
        await app.RunAsync();
    }
    private static InternalUser Current(HttpContext c) => (InternalUser)c.Items[UserKey]!;
    private static (int Id, string Name) TicketActor(HttpContext c) { var user = Current(c); return (CustomerPortalEndpoints.TicketId(user.Id), user.Name); }
    /// <summary>Assigning, closing, replying to the customer and changing status are the department head's (main's workflow; the service relies on the UI for assign).</summary>
    private static (int Id, string Name) TicketManager(HttpContext c) => Current(c).Permissions.Contains("ticket.manage.all")
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
    public sealed record LoginInput(string Username, string Password);
}

public sealed class StringLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var value) ? value : throw new JsonException("IDs must be strings.");
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
