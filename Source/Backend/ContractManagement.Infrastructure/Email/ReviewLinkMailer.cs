using System.Globalization;
using System.Net;
using ContractManagement.API.Domains.Email;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed record LinkMail(long ReviewId, string Recipient, string CustomerName, string ContractName, string Token, DateTime ExpiresAt, string? Message);

public sealed class ReviewLinkMailer(IEmailTransport email, IConfiguration config, IHostEnvironment environment, ILogger<ReviewLinkMailer> logger)
{
    /// <returns>False when the email could not be sent; the review stays open and the sale can resend.</returns>
    public async Task<bool> Send(LinkMail mail, string portalUrl, CancellationToken ct)
    {
        var url = $"{portalUrl.TrimEnd('/')}/review/{mail.Token}";
        if (environment.IsDevelopment() && config.GetValue<bool>("CustomerPortal:ConsoleDelivery")) Console.WriteLine($"[DEV] Link xem hop dong (review {mail.ReviewId}): {url}");
        // Vietnam has no daylight saving, so a fixed offset is exact.
        var expires = mail.ExpiresAt.AddHours(7).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) + " (giờ Việt Nam)";
        var note = string.IsNullOrWhiteSpace(mail.Message) ? "" : $"Lời nhắn: {mail.Message}\n\n";
        var text = $"Kính gửi {mail.CustomerName},\n\nBạn có hợp đồng \"{mail.ContractName}\" cần xem và phản hồi.\n\n{note}Mở link: {url}\nLink hết hạn lúc {expires}. Khi mở link, nhập số điện thoại trong hợp đồng để nhận mã xác nhận.";
        string H(string value) => WebUtility.HtmlEncode(value);
        var html = $"<p>Kính gửi {H(mail.CustomerName)},</p><p>Bạn có hợp đồng <strong>{H(mail.ContractName)}</strong> cần xem và phản hồi.</p>"
            + (note.Length == 0 ? "" : $"<p>Lời nhắn: {H(mail.Message!)}</p>")
            + $"<p><a href=\"{H(url)}\">Xem hợp đồng</a></p><p>Link hết hạn lúc {H(expires)}. Khi mở link, nhập số điện thoại trong hợp đồng để nhận mã xác nhận.</p>";
        try { await email.SendAsync(new(mail.Recipient, "Hợp đồng cần bạn xem: " + mail.ContractName.ReplaceLineEndings(" "), text, html), ct); return true; }
        catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning(e, "Review {ReviewId} link email failed.", mail.ReviewId); return false; }
    }
}
