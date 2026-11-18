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
    public async Task<bool> Send(long documentId, string subject, string message, CancellationToken ct)
    {
        try
        {
            var target = await (from d in db.ContractDocuments.AsNoTracking()
                join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
                where d.ContractDocumentId == documentId
                select new { d.ContractDocumentCode, d.ContractDocumentName, d.ContractDocumentCurrentVersionId, c.CustomerEmail, c.CustomerDisplayName }).SingleOrDefaultAsync(ct);
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
            var link = review is null ? $"{PortalUrl}/contracts" : $"{PortalUrl}/contracts/{review}";
            var greeting = $"Kính gửi {target.CustomerDisplayName},";
            var contract = $"Hợp đồng {target.ContractDocumentCode} · {target.ContractDocumentName}";
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
}
