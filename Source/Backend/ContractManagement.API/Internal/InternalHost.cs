using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Internal;

public static class InternalHost
{
    private const string UserKey = "Internal.EmployeeId.v1";
    public static async Task Run(WebApplicationBuilder builder, string[] args)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("MasterDatabase")))
            throw new InvalidOperationException("Internal mode requires ConnectionStrings:MasterDatabase.");
        builder.Services.AddMasterDatabase(builder.Configuration);
        builder.Services.AddScoped<IInternalRepository, InternalRepository>();
        builder.Services.AddScoped<InternalDraftService>();
        builder.Services.AddScoped<InternalBootstrap>();
        builder.Services.AddSingleton(new InternalTemplateStorage(builder.Configuration["InternalStorage:Root"]
            ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "private-storage", "internal-templates"))));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new StringLongConverter()));
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(o => { o.Cookie.Name = ".ConTrack.Internal"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.IdleTimeout = TimeSpan.FromHours(8); });
        builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = ".ConTrack.Internal.Csrf"; o.Cookie.SameSite = SameSiteMode.Strict; });
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("internal-login", context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new() {
                PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
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
            catch (InternalAppException e) { context.Response.StatusCode = e.Status; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            catch (ArgumentException e) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            catch (BadHttpRequestException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu gửi lên không hợp lệ." }); }
            catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Phiên bảo vệ đã hết hạn. Tải lại trang và thử lại." }); }
            catch (DbUpdateException e) { app.Logger.LogWarning(e, "Internal database write failed"); context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu trùng hoặc đã thay đổi. Kiểm tra rồi thử lại." }); }
            catch (Exception e) when (!context.RequestAborted.IsCancellationRequested) { app.Logger.LogError(e, "Internal request failed"); context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { message = "Không thể xử lý yêu cầu. Dữ liệu chưa được xác nhận lưu." }); }
        });
        app.UseSession();
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
        secured.MapGet("/contract-templates/{id:long}", async (long id, IInternalRepository r, CancellationToken ct) =>
            (await r.Templates(ct)).FirstOrDefault(t => t.Id == id) is { } t ? Results.Ok(t) : Results.NotFound());
        secured.MapGet("/customers", async (string? search, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Customers(search, ct)));
        secured.MapPost("/customers", async (CustomerInput input, HttpContext c, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.CreateCustomer(input, Current(c).Id, ct)));
        secured.MapGet("/products", async (string? search, IInternalRepository r, CancellationToken ct) => Results.Ok(await r.Products(search, ct)));
        secured.MapGet("/employees", async (HttpContext c, IInternalRepository r, CancellationToken ct) => {
            if (!Current(c).CanManageAll) throw new InternalAppException(403, "Bạn không có quyền xem danh sách nhân viên.");
            return Results.Ok(await r.Employees(ct));
        });
        secured.MapGet("/contract-drafts", async (string? search, string? ownerId, HttpContext c, IInternalRepository r, CancellationToken ct) =>
        {
            var user = Current(c); long? owner = user.CanManageAll ? null : user.Id;
            if (!string.IsNullOrEmpty(ownerId)) { if (!user.CanManageAll) throw new InternalAppException(403, "Không được lọc nháp của nhân viên khác.");
                if (!long.TryParse(ownerId, out var parsed) || parsed <= 0) throw new ArgumentException("Nhân viên không hợp lệ."); owner = parsed; }
            return Results.Ok(await r.Drafts(owner, search, ct));
        });
        secured.MapGet("/contract-drafts/{id:long}", async (long id, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Get(id, Current(c), ct)));
        secured.MapPost("/contract-drafts", async (SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(null, request, Current(c), ct)));
        secured.MapPut("/contract-drafts/{id:long}", async (long id, SaveDraftRequest request, HttpContext c, InternalDraftService s, CancellationToken ct) => Results.Ok(await s.Save(id, request, Current(c), ct)));
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
