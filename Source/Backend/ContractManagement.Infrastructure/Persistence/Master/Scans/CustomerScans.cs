using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>A scan a customer may see, and why it is theirs.</summary>
public sealed record CustomerScanMatch(long FileId, string Kind, long TargetId, string MatchedBy, string Status = "Ready");

/// <summary>
/// Which stored scans belong to a customer (017-customer-file-keys.sql):
/// <list type="bullet">
/// <item>a paper-signing evidence accepted as valid, of a contract of the customer (luồng A);</item>
/// <item>an old paper contract scanned in (luồng B) that staff confirmed for the customer, or whose OCR keywords hold
/// the customer's tax code (own or of one of their businesses), phone or email. The name alone is not enough: two
/// companies may have close names, and a wrong match would show one customer's contract to another.</item>
/// </list>
/// </summary>
public static class CustomerScans
{
    public const string EvidenceKind = "evidence";
    public const string ScanKind = "scan";
    private const int ValidEvidence = 2;
    private const int TaxCode = (int)ScanKeywordType.TaxCode, Phone = (int)ScanKeywordType.PhoneNumber, Email = (int)ScanKeywordType.Email;

    private sealed record Identity(string? Phone, string? Email, IReadOnlyList<string> TaxCodes);

    /// <summary>
    /// The contracts a customer may see on the portal: sent to them for review at least once, or already waiting for
    /// signature, signed or completed. A draft the company has not approved and sent stays hidden: nothing reaches the
    /// customer before Hành chính approves it.
    /// </summary>
    public static IQueryable<ContractDocument> SentToCustomer(MasterDbContext db, long customerId) =>
        db.ContractDocuments.AsNoTracking().Where(document => document.CustomerId == customerId && document.TenantId == null
            && ((document.ContractDocumentStatus >= (int)ContractDocumentStatus.PendingSignature && document.ContractDocumentStatus <= (int)ContractDocumentStatus.Completed)
                || db.ContractReviewRequests.Any(review => review.ContractDocumentId == document.ContractDocumentId)));

    public static async Task<IReadOnlyList<CustomerScanMatch>> ForCustomerAsync(MasterDbContext db, long customerId, CancellationToken cancellationToken)
    {
        var who = await IdentityAsync(db, customerId, cancellationToken);
        if (who is null)
        {
            return [];
        }

        var evidences = await (
                from evidence in db.ContractSignedEvidences.AsNoTracking()
                join file in db.ContractDocumentFiles on evidence.ContractDocumentFileId equals file.ContractDocumentFileId
                join document in db.ContractDocuments on file.ContractDocumentId equals document.ContractDocumentId
                where document.CustomerId == customerId && evidence.ContractSignedEvidenceStatus == ValidEvidence
                select new CustomerScanMatch(file.FileStorageFileid, EvidenceKind, evidence.ContractSignedEvidenceId, "Hợp đồng của bạn",
                    "Accepted"))
            .ToListAsync(cancellationToken);

        // Direct customer uploads are ordinary contract files. They are not signed-evidence records and never enter review.
        var customerUploads = await (
                from documentFile in db.ContractDocumentFiles.AsNoTracking()
                join file in db.FileStorages.AsNoTracking() on documentFile.FileStorageFileid equals file.FileStorageFileid
                join document in db.ContractDocuments.AsNoTracking() on documentFile.ContractDocumentId equals document.ContractDocumentId
                where document.CustomerId == customerId && file.FileStorageUploadedbycustomerid == customerId
                select new CustomerScanMatch(file.FileStorageFileid, "customer-upload", documentFile.ContractDocumentFileId,
                    "Bạn đã tải lên", "Ready"))
            .ToListAsync(cancellationToken);

        var jobs = db.OcrJobs.AsNoTracking().Where(job => job.ContractDocumentId == null && job.OcrJobProcessingStatus == "SUCCESS");
        var confirmed = await (
                from job in jobs
                join contract in db.Contracts on job.FileStorageFileid equals contract.FileStorageFileid
                where contract.PartnerId == customerId
                select new CustomerScanMatch(job.FileStorageFileid, ScanKind, job.OcrJobId, "Hợp đồng đã xác nhận cho bạn"))
            .ToListAsync(cancellationToken);

        var keywords = await (
                from job in jobs
                join keyword in db.ContractScanKeywords on job.FileStorageFileid equals keyword.FileStorageFileid
                where (keyword.ContractScanKeywordType == TaxCode && who.TaxCodes.Contains(keyword.ContractScanKeywordValue))
                    || (who.Phone != null && keyword.ContractScanKeywordType == Phone && keyword.ContractScanKeywordValue == who.Phone)
                    || (who.Email != null && keyword.ContractScanKeywordType == Email && keyword.ContractScanKeywordValue == who.Email)
                select new { job.FileStorageFileid, job.OcrJobId, keyword.ContractScanKeywordType })
            .ToListAsync(cancellationToken);
        var byKeyword = keywords
            .GroupBy(row => (row.FileStorageFileid, row.OcrJobId))
            .Select(group => new CustomerScanMatch(group.Key.FileStorageFileid, ScanKind, group.Key.OcrJobId,
                "Trùng " + string.Join(", ", group.Select(row => row.ContractScanKeywordType).Distinct().Order().Select(Label))));

        return evidences.Concat(customerUploads).Concat(confirmed).Concat(byKeyword)
            .GroupBy(match => match.FileId)
            .Select(group => group.FirstOrDefault(match => match.Kind == EvidenceKind && match.Status == "Accepted") ?? group.First())
            .ToList();
    }

    /// <summary>The customers a stored scan belongs to, by the same rules.</summary>
    public static async Task<IReadOnlyList<long>> CustomersForFileAsync(MasterDbContext db, long fileId, CancellationToken cancellationToken)
    {
        var owners = await (
                from file in db.ContractDocumentFiles.AsNoTracking()
                join evidence in db.ContractSignedEvidences on file.ContractDocumentFileId equals evidence.ContractDocumentFileId
                join document in db.ContractDocuments on file.ContractDocumentId equals document.ContractDocumentId
                where file.FileStorageFileid == fileId && evidence.ContractSignedEvidenceStatus == ValidEvidence
                select document.CustomerId)
            .ToListAsync(cancellationToken);

        var isScan = await db.OcrJobs.AnyAsync(job => job.FileStorageFileid == fileId && job.ContractDocumentId == null && job.OcrJobProcessingStatus == "SUCCESS", cancellationToken);
        if (isScan)
        {
            owners.AddRange(await db.Contracts.AsNoTracking()
                .Where(contract => contract.FileStorageFileid == fileId && contract.PartnerId != null)
                .Select(contract => contract.PartnerId!.Value)
                .ToListAsync(cancellationToken));
            var words = await db.ContractScanKeywords.AsNoTracking()
                .Where(keyword => keyword.FileStorageFileid == fileId)
                .Select(keyword => new { keyword.ContractScanKeywordType, keyword.ContractScanKeywordValue })
                .ToListAsync(cancellationToken);
            var taxes = words.Where(w => w.ContractScanKeywordType == TaxCode).Select(w => w.ContractScanKeywordValue).ToList();
            var phones = words.Where(w => w.ContractScanKeywordType == Phone).Select(w => w.ContractScanKeywordValue).ToList();
            var emails = words.Where(w => w.ContractScanKeywordType == Email).Select(w => w.ContractScanKeywordValue).ToList();
            if (words.Count > 0)
            {
                owners.AddRange(await db.Customers.AsNoTracking()
                    .Where(customer => customer.CustomerStatus == 1 && (phones.Contains(customer.CustomerPersonalPhone)
                        || (customer.CustomerEmail != null && emails.Contains(customer.CustomerEmail))
                        || (customer.CustomerPersonalTaxIdentifier != null && taxes.Contains(customer.CustomerPersonalTaxIdentifier))
                        || db.CustomerBusinesses.Any(b => b.CustomerId == customer.CustomerId && taxes.Contains(b.CustomerBusinessCompanyTaxIdentifier))))
                    .Select(customer => customer.CustomerId)
                    .ToListAsync(cancellationToken));
            }
        }

        return owners.Distinct().ToList();
    }

    /// <summary>
    /// Wraps an opened file's key for each customer it belongs to who has a key and was not given it yet. Adds the rows;
    /// the caller saves them.
    /// </summary>
    public static async Task GrantToCustomersAsync(MasterDbContext db, long fileId, byte[] fileKey, CancellationToken cancellationToken)
    {
        var customers = await CustomersForFileAsync(db, fileId, cancellationToken);
        if (customers.Count == 0)
        {
            return;
        }

        var keys = await db.CustomerEncryptionKeys.AsNoTracking()
            .Where(key => customers.Contains(key.CustomerId) && key.CustomerEncryptionKeyStatus == FileProtectionService.ActiveStatus)
            .Where(key => !db.CustomerFileKeys.Any(held => held.FileStorageFileid == fileId && held.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId))
            .Select(key => new { key.CustomerEncryptionKeyId, key.CustomerEncryptionKeyPublickey })
            .ToListAsync(cancellationToken);
        foreach (var key in keys.Where(key => !db.CustomerFileKeys.Local.Any(row => row.FileStorageFileid == fileId && row.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId)))
        {
            db.CustomerFileKeys.Add(new CustomerFileKey
            {
                FileStorageFileid = fileId,
                CustomerEncryptionKeyId = key.CustomerEncryptionKeyId,
                CustomerFileKeyWrappedkey = FileCrypto.Wrap(fileKey, key.CustomerEncryptionKeyPublickey),
                GrantedAt = DateTime.UtcNow,
            });
        }
    }

    public static async Task GrantToCustomerAsync(MasterDbContext db, long fileId, long customerId, byte[] fileKey, CancellationToken cancellationToken)
    {
        var key = await db.CustomerEncryptionKeys.AsNoTracking()
            .Where(candidate => candidate.CustomerId == customerId && candidate.CustomerEncryptionKeyStatus == FileProtectionService.ActiveStatus)
            .Select(candidate => new { candidate.CustomerEncryptionKeyId, candidate.CustomerEncryptionKeyPublickey })
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null || await db.CustomerFileKeys.AnyAsync(row => row.FileStorageFileid == fileId && row.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId, cancellationToken))
            return;
        db.CustomerFileKeys.Add(new CustomerFileKey
        {
            FileStorageFileid = fileId,
            CustomerEncryptionKeyId = key.CustomerEncryptionKeyId,
            CustomerFileKeyWrappedkey = FileCrypto.Wrap(fileKey, key.CustomerEncryptionKeyPublickey),
            GrantedAt = DateTime.UtcNow,
        });
    }

    private static async Task<Identity?> IdentityAsync(MasterDbContext db, long customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == customerId && c.CustomerStatus == 1)
            .Select(c => new { c.CustomerPersonalPhone, c.CustomerEmail, c.CustomerPersonalTaxIdentifier })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var taxes = await db.CustomerBusinesses.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .Select(b => b.CustomerBusinessCompanyTaxIdentifier)
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(customer.CustomerPersonalTaxIdentifier))
        {
            taxes.Add(customer.CustomerPersonalTaxIdentifier);
        }

        var phone = new string((customer.CustomerPersonalPhone ?? "").Where(char.IsAsciiDigit).ToArray());
        var email = customer.CustomerEmail?.Trim().ToLowerInvariant();
        return new Identity(phone.Length >= 9 ? phone : null, string.IsNullOrEmpty(email) ? null : email,
            taxes.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList());
    }

    private static string Label(int type) => type switch
    {
        TaxCode => "mã số thuế",
        Phone => "số điện thoại",
        Email => "email",
        _ => "thông tin",
    };
}
