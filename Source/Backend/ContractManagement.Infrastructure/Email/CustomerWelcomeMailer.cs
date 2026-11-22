using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using System.Net;
using Microsoft.AspNetCore.Identity;
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

    /// <summary>
    /// Hành chính sets the customer password and sends login credentials with portal link.
    /// The customer is forced to change password upon first login (CustomerPersonalPhoneVerifiedAt = null).
    /// </summary>
    public async Task<string> SendCredentialsAsync(long customerId, string password, string? phoneOverride, long actorId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .SingleOrDefaultAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");

        if (!SmtpEmailOptions.IsEmailAddress(customer.CustomerEmail))
            throw new InternalAppException(409, "Khách hàng chưa có email hợp lệ. Hãy thêm/cập nhật email cho khách trước.");

        if (!smtp.Value.IsConfigured())
            throw new InternalAppException(503, "Máy chủ chưa cấu hình gửi email (Email:Smtp). Hãy báo quản trị.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ArgumentException("Mật khẩu phải có ít nhất 6 ký tự.");

        if (!string.IsNullOrWhiteSpace(phoneOverride))
        {
            var normalizedPhone = PhoneNumber.Normalize(phoneOverride);
            if (normalizedPhone is not null && normalizedPhone != customer.CustomerPersonalPhone)
                customer.CustomerPersonalPhone = normalizedPhone;
        }

        var phone = PhoneNumber.Normalize(customer.CustomerPersonalPhone)
            ?? throw new InternalAppException(409, "Khách hàng chưa có số điện thoại hợp lệ.");

        var now = DateTime.UtcNow;
        customer.CustomerPasswordHash = new PasswordHasher<Customer>().HashPassword(customer, password);
        // Bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên
        customer.CustomerPersonalPhoneVerifiedAt = null;
        customer.UpdatedAt = now;
        customer.UpdatedBy = actorId;

        ReviewData.Audit(db, ReviewData.AccountCreatedAction, "CUSTOMER", customerId, "EMPLOYEE", actorId, now);
        await db.SaveChangesAsync(cancellationToken);

        var portalLoginUrl = $"{PortalUrl}/login";
        var name = customer.CustomerDisplayName;
        var text = $"Kính gửi quý khách {name},\n\n"
            + "Phòng Hành chính - Pháp chế đã cấp và kích hoạt tài khoản Cổng khách hàng cho quý khách trên hệ thống ConTrack.\n\n"
            + "THÔNG TIN ĐĂNG NHẬP:\n"
            + $"- Đường dẫn đăng nhập: {portalLoginUrl}\n"
            + $"- Tên đăng nhập (Số điện thoại): {phone}\n"
            + $"- Mật khẩu ban đầu: {password}\n\n"
            + "LƯU Ý BẢO MẬT:\n"
            + "Vì lý do an toàn thông tin, sau khi đăng nhập thành công lần đầu bằng mật khẩu trên, hệ thống sẽ yêu cầu quý khách THAY ĐỔI MẬT KHẨU NGAY để hoàn tất kích hoạt tài khoản.\n\n"
            + "Trân trọng,\nBan Quản trị ConTrack";

        var html = $"""
            <div style="font-family:Arial,sans-serif;font-size:14px;color:#111827;max-width:600px;line-height:1.6;margin:0 auto;border:1px solid #e5e7eb;border-radius:8px;padding:24px;background:#ffffff">
              <h2 style="color:#1d4ed8;margin-top:0;font-size:20px;border-bottom:2px solid #e5e7eb;padding-bottom:12px">Thông Tin Tài Khoản Cổng Khách Hàng</h2>
              <p>Kính gửi quý khách <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
              <p>Phòng Hành chính - Pháp chế đã cấp và kích hoạt tài khoản Cổng khách hàng cho quý khách trên hệ thống <strong>ConTrack</strong>.</p>

              <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:16px;margin:20px 0">
                <p style="margin:0 0 8px 0;font-size:15px;font-weight:bold;color:#0f172a">Thông tin đăng nhập:</p>
                <p style="margin:4px 0"><strong>Tên đăng nhập (SĐT):</strong> <code style="background:#e2e8f0;padding:2px 6px;border-radius:4px;color:#0f172a">{WebUtility.HtmlEncode(phone)}</code></p>
                <p style="margin:4px 0"><strong>Mật khẩu ban đầu:</strong> <code style="background:#e2e8f0;padding:2px 6px;border-radius:4px;color:#0f172a">{WebUtility.HtmlEncode(password)}</code></p>
              </div>

              <div style="text-align:center;margin:28px 0">
                <a href="{WebUtility.HtmlEncode(portalLoginUrl)}" style="background:#2563eb;color:#ffffff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;display:inline-block;box-shadow:0 1px 2px rgba(0,0,0,0.05)">Đăng Nhập Cổng Khách Hàng</a>
              </div>

              <div style="background:#fef3c7;border-left:4px solid #f59e0b;padding:12px 16px;margin:20px 0;border-radius:0 4px 4px 0">
                <p style="margin:0;color:#92400e;font-size:13px">
                  <strong>Lưu ý bảo mật quan trọng:</strong> Vì lý do an toàn thông tin, ngay sau khi đăng nhập thành công lần đầu bằng mật khẩu trên, hệ thống sẽ yêu cầu quý khách <strong>thay đổi mật khẩu mới</strong> để hoàn tất xác thực tài khoản.
                </p>
              </div>

              <p style="color:#6b7280;font-size:12px;margin-top:24px;border-top:1px solid #e5e7eb;padding-top:12px">
                Nếu quý khách không phải khách hàng của công ty hoặc có thắc mắc, vui lòng liên hệ bộ phận hỗ trợ của chúng tôi.
              </p>
            </div>
            """;

        await email.SendAsync(new EmailMessage(customer.CustomerEmail!, "Thông tin tài khoản Cổng khách hàng ConTrack", text, html), cancellationToken);
        return FileKeyOwnerService.Mask(customer.CustomerEmail)!;
    }

    /// <summary>0912345001 to 091****001.</summary>
    internal static string MaskPhone(string phone) =>
        phone.Length <= 6 ? new string('*', phone.Length) : phone[..3] + new string('*', phone.Length - 6) + phone[^3..];
}
