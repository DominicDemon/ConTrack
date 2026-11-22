using System.Text.Json;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public interface ICustomerOtpMail
{
    /// <returns>The masked address the code went to.</returns>
    Task<string> Send(long documentId, string otp, DateTime expiresAt, CancellationToken ct);
    Task<string> SendForReview(long reviewId, string otp, DateTime expiresAt, CancellationToken ct);
}

/// <summary>Emails a customer's one-time code (review link and contract signing) to the contract's customer email.</summary>
public sealed class CustomerOtpMailer(MasterDbContext db, IEmailTransport email, ILogger<CustomerOtpMailer> logger) : ICustomerOtpMail
{
    /// <summary>The contract's customer email, else the customer's own; null when neither is a valid address.</summary>
    public async Task<string?> Address(long documentId, CancellationToken ct)
    {
        var target = await (from d in db.ContractDocuments.AsNoTracking()
            join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
            where d.ContractDocumentId == documentId
            select new { d.ContractDocumentCurrentVersionId, c.CustomerEmail }).SingleOrDefaultAsync(ct);
        if (target is null) return null;
        var snapshot = await db.ContractDynamicValues.AsNoTracking().Where(v => v.ContractDocumentId == documentId
            && v.ContractVersionId == target.ContractDocumentCurrentVersionId && v.FieldKey == InternalRepository.SnapshotKey)
            .Select(v => v.FieldValue).FirstOrDefaultAsync(ct);
        var fields = snapshot is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<DraftContent>(snapshot, InternalRepository.Json)?.Fields ?? [];
        return ReviewLink.ContactEmail(fields, target.CustomerEmail);
    }

    public async Task<string> SendForReview(long reviewId, string otp, DateTime expiresAt, CancellationToken ct)
    {
        var documentId = await db.ContractReviewRequests.AsNoTracking().Where(r => r.ContractReviewRequestId == reviewId)
            .Select(r => (long?)r.ContractDocumentId).SingleOrDefaultAsync(ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        return await Send(documentId, otp, expiresAt, ct);
    }

    /// <summary>Sends the code; throws 422 when the contract has no email and 503 when sending fails.</summary>
    /// <returns>The masked address, for telling the customer where to look.</returns>
    public async Task<string> Send(long documentId, string otp, DateTime expiresAt, CancellationToken ct)
    {
        var address = await Address(documentId, ct)
            ?? throw new InternalAppException(422, "Hợp đồng chưa có email của khách hàng để gửi mã OTP. Vui lòng liên hệ nhân viên phụ trách.");
        try
        {
            await email.SendAsync(new EmailMessage(address, CustomerOtpEmailTemplate.Subject,
                CustomerOtpEmailTemplate.BuildPlainTextBody(otp, expiresAt),
                CustomerOtpEmailTemplate.BuildBody(otp, expiresAt), CustomerOtpEmailTemplate.LogoContentId), ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Customer OTP email for contract {DocumentId} failed.", documentId);
            throw new InternalAppException(503, "Không gửi được mã xác nhận qua email. Vui lòng thử lại sau.");
        }
        return ReviewLink.MaskEmail(address);
    }
}
