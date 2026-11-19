using System.Net;
using System.Text.Json;
using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>
/// Emails the customer when something on their contract needs them or is done (staff reply, company approval, company
/// signed, contract signed). Called after the action has committed; a failed email is only logged.
/// shortcut: no outbox, so an email lost to an SMTP outage is not retried; add a table and a worker if that matters.
/// </summary>
public sealed class CustomerContractMailer(MasterDbContext db, IEmailTransport email, IConfiguration config, ILogger<CustomerContractMailer> logger)
{
    private string PortalUrl => (config["CustomerPortal:BaseUrl"] ?? "http://localhost:3000/portal").TrimEnd('/');

    /// <summary>Sends one email about the contract's latest review; returns false when there is no address or sending failed.</summary>
    /// <param name="prospectMessage">Wording for a customer without a portal account; defaults to <paramref name="message"/>.</param>
    public async Task<bool> Send(long documentId, string subject, string message, CancellationToken ct, string? prospectMessage = null)
    {
        try
        {
            var target = await (from d in db.ContractDocuments.AsNoTracking()
                join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
                where d.ContractDocumentId == documentId
                select new { d.ContractDocumentCode, d.ContractDocumentName, d.ContractDocumentCurrentVersionId, c.CustomerId, c.CustomerEmail, c.CustomerDisplayName }).SingleOrDefaultAsync(ct);
            if (target is null) return false;
            var review = await db.ContractReviewRequests.AsNoTracking().Where(r => r.ContractDocumentId == documentId)
                .OrderByDescending(r => r.ContractReviewRequestId).Select(r => (long?)r.ContractReviewRequestId).FirstOrDefaultAsync(ct);
            var snapshot = await db.ContractDynamicValues.AsNoTracking().Where(v => v.ContractDocumentId == documentId
                && v.ContractVersionId == target.ContractDocumentCurrentVersionId && v.FieldKey == InternalRepository.SnapshotKey)
                .Select(v => v.FieldValue).FirstOrDefaultAsync(ct);
            var fields = snapshot is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<DraftContent>(snapshot, InternalRepository.Json)?.Fields ?? [];
            // Same address as the review link email: the contract's customer email, else the customer's own.
            var address = ReviewLink.ContactEmail(fields, target.CustomerEmail);
            if (address is null) { logger.LogInformation("Hợp đồng {Code}: khách chưa có email, chỉ báo trên Portal.", target.ContractDocumentCode); return false; }
            var greeting = $"Kính gửi {target.CustomerDisplayName},";
            var contract = $"Hợp đồng {target.ContractDocumentCode} · {target.ContractDocumentName}";
            if (!await ReviewData.HasAccount(db, target.CustomerId, ct))
            {
                // A prospect has no portal login: the raw link token is not stored, so point them back to the link email.
                var hint = "Mở lại link xem hợp đồng trong email chúng tôi đã gửi (xác minh bằng mã OTP gửi về số điện thoại). Nếu link đã hết hạn, vui lòng liên hệ nhân viên phụ trách để nhận link mới.";
                await email.SendAsync(new EmailMessage(address, subject, $"{greeting}\n\n{contract}\n{prospectMessage ?? message}\n\n{hint}",
                    $"<p>{WebUtility.HtmlEncode(greeting)}</p><p><strong>{WebUtility.HtmlEncode(contract)}</strong></p><p>{WebUtility.HtmlEncode(prospectMessage ?? message)}</p><p>{WebUtility.HtmlEncode(hint)}</p>"), ct);
                return true;
            }
            var link = review is null ? $"{PortalUrl}/contracts" : $"{PortalUrl}/contracts/{review}";
            await email.SendAsync(new EmailMessage(address, subject,
                $"{greeting}\n\n{contract}\n{message}\n\nMở hợp đồng: {link}",
                $"<p>{WebUtility.HtmlEncode(greeting)}</p><p><strong>{WebUtility.HtmlEncode(contract)}</strong></p><p>{WebUtility.HtmlEncode(message)}</p>"
                + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Mở hợp đồng trên Cổng khách hàng</a></p>"), ct);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Không gửi được email cho khách về hợp đồng {DocumentId}: {Subject}.", documentId, subject);
            return false;
        }
    }

    /// <summary>
    /// The portal login (phone) and a temporary password, in one email as the business chose. The password is never logged.
    /// shortcut: anyone reading the mailbox can log in until the customer changes the password; switch the password to SMS if that matters.
    /// </summary>
    public async Task<bool> SendAccount(string address, string customerName, string phone, string password, int hours, bool created, CancellationToken ct)
    {
        try
        {
            var subject = created ? "Tài khoản Cổng khách hàng của bạn" : "Mật khẩu tạm mới cho Cổng khách hàng";
            var login = $"{PortalUrl}/login";
            var intro = created ? "Hợp đồng giữa bạn và công ty đã được hai bên ký. Công ty đã tạo tài khoản Cổng khách hàng để bạn xem lại hợp đồng, tải bản đã ký và nhận các hợp đồng sau."
                : "Công ty đã cấp lại mật khẩu tạm cho tài khoản Cổng khách hàng của bạn.";
            var rules = $"Mật khẩu tạm hết hạn sau {hours} giờ. Lần đăng nhập đầu tiên bạn cần đổi sang mật khẩu mới của riêng bạn.";
            static string e(string value) => WebUtility.HtmlEncode(value);
            await email.SendAsync(new EmailMessage(address, subject,
                $"Kính gửi {customerName},\n\n{intro}\n\nĐăng nhập: {login}\nTên đăng nhập (số điện thoại): {phone}\nMật khẩu tạm: {password}\n\n{rules}",
                $"<p>Kính gửi {e(customerName)},</p><p>{e(intro)}</p><p>Tên đăng nhập (số điện thoại): <strong>{e(phone)}</strong><br>Mật khẩu tạm: <strong>{e(password)}</strong></p>"
                + $"<p><a href=\"{e(login)}\">Đăng nhập Cổng khách hàng</a></p><p>{e(rules)}</p>"), ct);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Không gửi được email tài khoản cho khách.");
            return false;
        }
    }
}
