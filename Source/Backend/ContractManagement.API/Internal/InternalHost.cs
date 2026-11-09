using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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
    private const string UserKey = "Internal.EmployeeId.v1";
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
        builder.Services.AddScoped<InternalBootstrap>();
        builder.Services.AddScoped<IInternalTemplateImport, InternalTemplateImport>();
        builder.Services.AddScoped<IContractReviewRepository, ContractReviewRepository>();
        builder.Services.AddScoped<ICustomerPortalRepository, CustomerPortalRepository>();
        builder.Services.AddScoped<SmsCallbackProcessor>();
        builder.Services.Configure<EsmsOptions>(builder.Configuration.GetSection("Esms"));
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<SmsOutboxWriter>();
        if (builder.Configuration.GetValue<bool>("Esms:Enabled")) builder.Services.AddHttpClient<ISmsSender, EsmsSmsSender>(h => h.Timeout = TimeSpan.FromSeconds(30));
        else builder.Services.AddSingleton<ISmsSender, FakeSmsSender>();
        builder.Services.AddHostedService<SmsOutboxWorker>();
        builder.Services.AddMemoryCache();
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
        if (args.Contains("--seed-internal"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InternalBootstrap>().Run(
                builder.Configuration["InternalBootstrap:SalePassword"] ?? "", builder.Configuration["InternalBootstrap:AdminPassword"] ?? "");
            Console.WriteLine("Internal schema, roles, accounts and demo template initialized. Existing passwords were preserved.");
            return;
        }
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
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
            if (user is null || !user.CanWrite) throw new InternalAppException(401, "Tên đăng nhập hoặc mật khẩu không đúng.");
            context.Session.Clear(); context.Session.SetString(UserKey, user.Id.ToString());
            return Results.Ok(user);
        }).RequireRateLimiting("internal-login");
        api.MapGet("/auth/me", async (HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await User(c, r, ct)));
        api.MapPost("/auth/logout", async (HttpContext c, IAntiforgery csrf) => { await csrf.ValidateRequestAsync(c); c.Session.Clear(); return Results.NoContent(); });
        var secured = api.MapGroup("").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var user = await User(c, c.RequestServices.GetRequiredService<IInternalRepository>(), c.RequestAborted);
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
        secured.MapPost("/customers/{id:long}/reset-temp-password", async (long id, HttpContext c, IContractReviewRepository r, CancellationToken ct) =>
        { await r.ResetTemporaryPassword(id, Current(c), ct); return Results.NoContent(); });
        CustomerPortalEndpoints.Map(app);
        await app.RunAsync();
    }
    private static InternalUser Current(HttpContext c) => (InternalUser)c.Items[UserKey]!;
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
