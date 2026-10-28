using System.Net;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Branding;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// Email gửi tài khoản sau khi tạo tenant. Mật khẩu chỉ tồn tại trong email này (không lưu DB, không ghi log).
/// </summary>
public static class SubscriptionWelcomeEmailTemplate
{
    public const string Subject = "Tài khoản đăng nhập của tổ chức bạn đã sẵn sàng";

    public static EmailMessage Build(string recipient, string fullName, string tenantName, string tenantCode,
        string loginUrl, DateTime? endAt, string password)
    {
        var expiry = endAt is { } value ? value.AddHours(7).ToString("dd/MM/yyyy") : null;
        var text = $"""
            Xin chào {fullName},

            Tổ chức "{tenantName}" đã được khởi tạo trên {ApplicationBranding.Name}.

            Thông tin đăng nhập:
            - Mã tổ chức (Tenant code): {tenantCode}
            - Tên đăng nhập: {recipient}
            - Mật khẩu tạm: {password}
            - Trang đăng nhập: {loginUrl}
            {(expiry is null ? "" : $"- Gói dịch vụ hiệu lực đến: {expiry}")}

            Bạn cần nhập Mã tổ chức khi đăng nhập. Hệ thống sẽ yêu cầu đổi mật khẩu ở lần đăng nhập đầu tiên.
            Không chia sẻ email này cho người khác.
            """;

        string E(string s) => WebUtility.HtmlEncode(s);
        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#1f2937;line-height:1.6">
              <img src="cid:{CustomerOtpEmailTemplate.LogoContentId}" alt="{E(ApplicationBranding.Name)}" style="height:40px">
              <h2 style="margin:24px 0 8px">Tài khoản của bạn đã sẵn sàng</h2>
              <p>Xin chào {E(fullName)},</p>
              <p>Tổ chức <strong>{E(tenantName)}</strong> đã được khởi tạo trên {E(ApplicationBranding.Name)}.</p>
              <table style="border-collapse:collapse;width:100%;margin:16px 0">
                <tr><td style="padding:8px;border:1px solid #e5e7eb">Mã tổ chức (Tenant code)</td><td style="padding:8px;border:1px solid #e5e7eb"><strong>{E(tenantCode)}</strong></td></tr>
                <tr><td style="padding:8px;border:1px solid #e5e7eb">Tên đăng nhập</td><td style="padding:8px;border:1px solid #e5e7eb">{E(recipient)}</td></tr>
                <tr><td style="padding:8px;border:1px solid #e5e7eb">Mật khẩu tạm</td><td style="padding:8px;border:1px solid #e5e7eb;font-family:monospace">{E(password)}</td></tr>
                {(expiry is null ? "" : $"<tr><td style=\"padding:8px;border:1px solid #e5e7eb\">Hiệu lực đến</td><td style=\"padding:8px;border:1px solid #e5e7eb\">{expiry}</td></tr>")}
              </table>
              <p><a href="{E(loginUrl)}" style="display:inline-block;background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none">Đăng nhập</a></p>
              <p style="font-size:13px;color:#6b7280">Bạn cần nhập Mã tổ chức khi đăng nhập. Hệ thống sẽ yêu cầu đổi mật khẩu ở lần đăng nhập đầu tiên. Không chia sẻ email này cho người khác.</p>
            </div>
            """;
        return new EmailMessage(recipient, Subject, text, html, CustomerOtpEmailTemplate.LogoContentId);
    }
}
