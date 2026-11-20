using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Email;

/// <summary>
/// Tells a new customer that their portal account exists. The mail holds no password: its button opens the page where
/// the customer sets one with a code sent to this same address (CustomerProfileService.ForgotPasswordAsync).
/// </summary>
public sealed class CustomerWelcomeMailer(
    MasterDbContext db,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    IConfiguration config,
    ILogger<CustomerWelcomeMailer> logger)
{
    private string PortalUrl => (config["CustomerPortal:BaseUrl"] ?? "http://localhost:3000/portal").TrimEnd('/');

    /// <summary>After a customer is created: best effort, a mail that fails only goes to the log.</summary>
    public async Task TrySendAsync(long customerId, CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(customerId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Welcome mail to customer {CustomerId} was not sent.", customerId);
        }
    }

    /// <summary>Sends the mail; returns the masked address it went to. Staff use it again when the mail was lost.</summary>
    public async Task<string> SendAsync(long customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == customerId && c.CustomerStatus == 1)
            .Select(c => new { c.CustomerDisplayName, c.CustomerEmail, c.CustomerPersonalPhone })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");
        if (!SmtpEmailOptions.IsEmailAddress(customer.CustomerEmail))
        {
            throw new InternalAppException(409, "Khách hàng chưa có email hợp lệ. Hãy thêm email cho khách trước.");
        }

        if (!smtp.Value.IsConfigured())
        {
            throw new InternalAppException(503, "Máy chủ chưa cấu hình gửi email (Email:Smtp). Hãy báo quản trị.");
        }

        var link = $"{PortalUrl}/forgot-password?welcome=1";
        var name = customer.CustomerDisplayName;
        var phone = MaskPhone(customer.CustomerPersonalPhone);
        var text = $"Kính gửi {name},\n\nCông ty đã tạo tài khoản cổng khách hàng cho quý khách để xem hợp đồng, phản hồi và gửi yêu cầu hỗ trợ.\n"
            + $"Số điện thoại đăng nhập: {phone}\n\nĐặt mật khẩu tại: {link}\n"
            + "Trên trang đó, nhập số điện thoại đăng nhập; mã xác nhận sẽ được gửi tới email này.\n\n"
            + "Nếu quý khách không phải khách hàng của công ty, xin bỏ qua thư này.";
        var html = $"""
            <div style="font-family:Arial,sans-serif;font-size:14px;color:#111827;max-width:560px">
              <p>Kính gửi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
              <p>Công ty đã tạo tài khoản cổng khách hàng cho quý khách để xem hợp đồng, phản hồi và gửi yêu cầu hỗ trợ.</p>
              <p>Số điện thoại đăng nhập: <strong>{WebUtility.HtmlEncode(phone)}</strong></p>
              <p style="margin:24px 0"><a href="{WebUtility.HtmlEncode(link)}" style="background:#2563eb;color:#ffffff;padding:10px 18px;border-radius:6px;text-decoration:none;font-weight:bold">Đặt mật khẩu</a></p>
              <p>Trên trang đó, nhập số điện thoại đăng nhập; mã xác nhận sẽ được gửi tới email này.</p>
              <p style="color:#6b7280;font-size:12px">Nếu quý khách không phải khách hàng của công ty, xin bỏ qua thư này.</p>
            </div>
            """;
        await email.SendAsync(new EmailMessage(customer.CustomerEmail!, "Tài khoản cổng khách hàng của quý khách", text, html), cancellationToken);
        return FileKeyOwnerService.Mask(customer.CustomerEmail)!;
    }

    /// <summary>0912345001 to 091****001.</summary>
    internal static string MaskPhone(string phone) =>
        phone.Length <= 6 ? new string('*', phone.Length) : phone[..3] + new string('*', phone.Length - 6) + phone[^3..];
}
