using System.Net;
using ContractManagement.API.Domains.Support;
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
    /// <summary>Set by the email link + OTP: the session may only reach this one review, and only until the link expires.</summary>
    public const string LinkReviewKey = "Customer.LinkReview.v1";
    private const int MaxFailuresPerPhone = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly string[] OpenDuringPasswordChange = ["/auth/me", "/auth/change-password", "/auth/logout", "/auth/csrf"];

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/customer").AddEndpointFilter(async (invocation, next) =>
        {
            var c = invocation.HttpContext;
            var path = c.Request.Path.Value!["/api/customer".Length..];
            if (path is "/auth/csrf" or "/auth/login" || path.StartsWith("/link/")) return await next(invocation);
            if (!long.TryParse(c.Session.GetString(CustomerKey), out var id)) throw new InternalAppException(401, "Vui lòng đăng nhập.");
            var portal = c.RequestServices.GetRequiredService<ICustomerPortalRepository>();
            var customer = await portal.Get(id, c.RequestAborted);
            if (customer is null) { c.Session.Clear(); throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa."); }
            if (long.TryParse(c.Session.GetString(LinkReviewKey), out var linkReview))
            {
                if (!await portal.LinkActive(linkReview, c.RequestAborted)) { c.Session.Clear(); throw new InternalAppException(410, "Link đã hết hạn. Liên hệ nhân viên phụ trách để nhận link mới.", "LINK_EXPIRED"); }
                if (!(path is "/auth/me" or "/auth/logout" || path == $"/reviews/{linkReview}" || path.StartsWith($"/reviews/{linkReview}/")))
                    throw new InternalAppException(403, "Link này chỉ dùng để xem một hợp đồng.");
            }
            else if (customer.MustChangePassword && !OpenDuringPasswordChange.Contains(path))
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
        // The token travels in the body, not the URL, so it stays out of API request logs.
        api.MapPost("/link/status", async (ReviewLinkInput input, HttpContext c, IAntiforgery csrf, ICustomerPortalRepository r, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var link = await r.OpenLink(input.Token, ct);
            var verified = c.Session.GetString(LinkReviewKey) == link.ReviewId.ToString();
            return Results.Ok(new { verified, reviewId = verified ? link.ReviewId : (long?)null, link.ExpiresAt });
        }).RequireRateLimiting("customer-login");
        api.MapPost("/link/otp", async (ReviewLinkInput input, HttpContext c, IAntiforgery csrf, ICustomerPortalRepository r, ReviewLinkOtp otp, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            await otp.Issue(await r.OpenLink(input.Token, ct), input.Phone, ct);
            return Results.NoContent();
        }).RequireRateLimiting("customer-login");
        api.MapPost("/link/verify", async (ReviewLinkInput input, HttpContext c, IAntiforgery csrf, ICustomerPortalRepository r, ReviewLinkOtp otp, CancellationToken ct) =>
        {
            await csrf.ValidateRequestAsync(c);
            var link = await r.OpenLink(input.Token, ct);
            otp.Verify(link.ReviewId, input.Otp);
            await r.RecordLinkVerified(link, ct);
            c.Session.Clear(); c.Session.SetString(CustomerKey, link.CustomerId.ToString()); c.Session.SetString(LinkReviewKey, link.ReviewId.ToString());
            return Results.Ok(new { reviewId = link.ReviewId });
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
        api.MapGet("/signing/{reviewId:long}", async (long reviewId, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.PortalState(Current(c).Id, reviewId, ct)));
        api.MapPost("/signing/{reviewId:long}/otp/request", async (long reviewId, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.Ok(await signing.RequestPortalOtp(Current(c).Id, reviewId, ct)));
        api.MapPost("/signing/{reviewId:long}/otp/verify", async (long reviewId, CustomerSigningOtpVerification input, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
        { await signing.VerifyPortalOtp(Current(c).Id, reviewId, input, ct); return Results.NoContent(); });
        api.MapGet("/signing/{reviewId:long}/document", async (long reviewId, string challengeId, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.File(await signing.PortalDocument(Current(c).Id, reviewId, challengeId, ct), "application/pdf", "contract-company-signed.pdf"));
        api.MapGet("/signing/{reviewId:long}/final-document", async (long reviewId, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.File(await signing.PortalFinalDocument(Current(c).Id, reviewId, ct), "application/pdf", "signed-contract.pdf"));
        api.MapPost("/signing/{reviewId:long}/sign", async (long reviewId, CustomerSigningRequest input, HttpContext c, ICustomerSigningService signing, CancellationToken ct) =>
            Results.File(await signing.PortalSign(Current(c).Id, reviewId, input, c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers.UserAgent.ToString(), ct),
                "application/pdf", "signed-contract.pdf"));
        api.MapGet("/reviews", async (HttpContext c, ICustomerPortalRepository r, CancellationToken ct) => Results.Ok(await r.Reviews(Current(c).Id, ct)));
        api.MapGet("/reviews/{id:long}", async (long id, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) => Results.Ok(await r.Review(Current(c).Id, id, ct)));
        api.MapPost("/reviews/{id:long}/comments", async (long id, CommentInput input, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) =>
            Results.Ok(await r.Comment(Current(c).Id, id, input, ct)));
        api.MapPost("/reviews/{id:long}/respond", async (long id, CustomerRespondInput input, HttpContext c, ICustomerPortalRepository r, CancellationToken ct) =>
            Results.Ok(await r.Respond(Current(c).Id, id, input, c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers.UserAgent.ToString(), ct)));

        // Hỗ trợ: the customer is CUSTOMER.customer_id from the portal session; the service (from main) keeps the workflow. No tenant: DTC itself.
        api.MapGet("/support/services", async (ISupportTicketService s, CancellationToken ct) => Results.Ok(await s.GetAvailableServicesAsync(ct)));
        api.MapGet("/support/tickets", async (string? status, string? priority, string? keyword, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(await s.GetAllForTenantAsync(0, "", TicketId(Current(c).Id), status, priority, keyword, ct)));
        api.MapGet("/support/tickets/{id:int}", async (int id, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            await s.GetByIdForCustomerAsync(id, 0, "", TicketId(Current(c).Id), ct) is { } t ? Results.Ok(t) : throw new InternalAppException(404, "Không tìm thấy yêu cầu hỗ trợ."));
        api.MapPost("/support/tickets", async (CreateSupportTicketRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.ResolvedSubject) || string.IsNullOrWhiteSpace(input.ResolvedDescription)) throw new ArgumentException("Vui lòng nhập đầy đủ tiêu đề và nội dung yêu cầu hỗ trợ.");
            var customer = Current(c);
            return Results.Ok(await s.CreateCustomerTicketAsync(0, "", "", TicketId(customer.Id), customer.Name, input, ct));
        });
        api.MapPost("/support/tickets/{id:int}/messages", async (int id, CustomerReplyTicketRequestDto input, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            var customer = Current(c);
            return await s.AddCustomerMessageAsync(id, 0, "", "", TicketId(customer.Id), customer.Name, input, ct) is { } m ? Results.Ok(m) : throw new InternalAppException(404, "Không tìm thấy yêu cầu hỗ trợ.");
        });
        api.MapGet("/notifications", async (string? status, int? limit, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
        {
            var (items, unreadCount) = await s.GetCustomerNotificationsAsync(TicketId(Current(c).Id), status ?? "all", Math.Clamp(limit ?? 20, 1, 50), ct);
            return Results.Ok(new { items, unreadCount });
        });
        api.MapGet("/notifications/unread-count", async (HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(new { count = await s.GetCustomerUnreadNotificationCountAsync(TicketId(Current(c).Id), ct) }));
        api.MapPatch("/notifications/{id:int}/read", async (int id, HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            await s.MarkCustomerNotificationReadAsync(TicketId(Current(c).Id), id, ct) ? Results.NoContent() : throw new InternalAppException(404, "Không tìm thấy thông báo."));
        api.MapPost("/notifications/mark-all-read", async (HttpContext c, ISupportTicketService s, CancellationToken ct) =>
            Results.Ok(new { changed = await s.MarkAllCustomerNotificationsReadAsync(TicketId(Current(c).Id), ct) }));

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
    /// <summary>The ticket service (from main) keys people by int.</summary>
    internal static int TicketId(long id) => checked((int)id);
}
