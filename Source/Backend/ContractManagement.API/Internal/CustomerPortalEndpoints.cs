using System.Net;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Internal;

public static class CustomerPortalEndpoints
{
    public const string CustomerKey = "Customer.Id.v1";
    private const int MaxFailuresPerPhone = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly string[] OpenDuringPasswordChange = ["/auth/me", "/auth/change-password", "/auth/logout", "/auth/csrf"];

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/customer").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var path = c.Request.Path.Value!["/api/customer".Length..];
            if (path is "/auth/csrf" or "/auth/login") return await next(invocation);
            if (!long.TryParse(c.Session.GetString(CustomerKey), out var id)) throw new InternalAppException(401, "Vui lòng đăng nhập.");
            var customer = await c.RequestServices.GetRequiredService<ICustomerPortalRepository>().Get(id, c.RequestAborted);
            if (customer is null) { c.Session.Clear(); throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa."); }
            if (customer.MustChangePassword && !OpenDuringPasswordChange.Contains(path))
                throw new InternalAppException(403, "Bạn cần đổi mật khẩu tạm trước khi tiếp tục.", "PASSWORD_CHANGE_REQUIRED");
            c.Items[CustomerKey] = customer;
            if (!HttpMethods.IsGet(c.Request.Method)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        api.MapGet("/auth/csrf", (HttpContext c, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(c).RequestToken }));
        api.MapPost("/auth/login", async (CustomerLoginInput input, HttpContext c, IAntiforgery csrf, ICustomerPortalRepository r, IMemoryCache cache, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var phone = PhoneNumber.Normalize(input.Phone);
            if (phone is null || string.IsNullOrEmpty(input.Password) || input.Password.Length > 200)
                throw new InternalAppException(401, "Số điện thoại hoặc mật khẩu không đúng.");
            var failureKey = "customer-login-fail:" + phone;
            if (cache.TryGetValue(failureKey, out int failures) && failures >= MaxFailuresPerPhone)
                throw new InternalAppException(429, "Sai mật khẩu quá nhiều lần. Thử lại sau 15 phút.");
            var customer = await r.Login(phone, input.Password, ct);
            if (customer is null)
            {
                cache.Set(failureKey, failures + 1, FailureWindow);
                throw new InternalAppException(401, "Số điện thoại hoặc mật khẩu không đúng.");
            }
            cache.Remove(failureKey);
            c.Session.Clear(); c.Session.SetString(CustomerKey, customer.Id.ToString());
            return Results.Ok(customer);
        }).RequireRateLimiting("customer-login");
        api.MapGet("/auth/me", (HttpContext c) => Results.Ok(Current(c)));
        api.MapPost("/auth/logout", (HttpContext c) => { c.Session.Clear(); return Results.NoContent(); });
        api.MapPost("/auth/change-password", async (CustomerChangePasswordInput input, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) =>
        {
            var id = Current(c).Id;
            await r.ChangePassword(id, input, ct);
            c.Session.Clear(); c.Session.SetString(CustomerKey, id.ToString());
            return Results.Ok(await r.Get(id, ct));
        });
        api.MapGet("/reviews", async (HttpContext c, ICustomerPortalRepository r, CancellationToken ct) => Results.Ok(await r.Reviews(Current(c).Id, ct)));
        api.MapGet("/reviews/{id:long}", async (long id, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) => Results.Ok(await r.Review(Current(c).Id, id, ct)));
        api.MapPost("/reviews/{id:long}/comments", async (long id, CommentInput input, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) =>
            Results.Ok(await r.Comment(Current(c).Id, id, input, ct)));
        api.MapPost("/reviews/{id:long}/respond", async (long id, CustomerRespondInput input, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) =>
            Results.Ok(await r.Respond(Current(c).Id, id, input, c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers.UserAgent.ToString(), ct)));

        // eSMS delivery report: outside the session/CSRF groups, accepted only from the provider's IPs.
        app.MapGet("/api/sms/esms/callback", async (HttpContext c, SmsCallbackProcessor processor, IOptions<EsmsOptions> options, ILogger<SmsCallbackProcessor> logger) =>
        {
            var ip = c.Connection.RemoteIpAddress;
            var allowed = options.Value.AllowAnyCallbackIp || options.Value.AllowedCallbackIps.Any(a => IPAddress.TryParse(a, out var x) && ip is not null
                && (x.Equals(ip) || (ip.IsIPv4MappedToIPv6 && x.Equals(ip.MapToIPv4()))));
            if (!allowed) return Results.Ok();
            try { await processor.Process(c.Request.Query, c.Request.QueryString.Value ?? "", ip?.ToString(), c.RequestAborted); }
            catch (Exception e) when (!c.RequestAborted.IsCancellationRequested) { logger.LogWarning(e, "eSMS callback processing failed."); }
            return Results.Ok();
        });
    }

    private static CustomerSession Current(HttpContext c) => (CustomerSession)c.Items[CustomerKey]!;
}
