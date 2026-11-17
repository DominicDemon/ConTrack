using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Files;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class CustomerSigningService(
    MasterDbContext db,
    InternalTemplateStorage storage,
    IElectronicSigningPdfService signatures,
    SmsOutboxWriter sms,
    IDataProtectionProvider protection,
    IHostEnvironment environment,
    ILogger<CustomerSigningService> logger,
    IOptions<EsmsOptions> smsOptions) : ICustomerSigningService
{
    private const int CustomerParty = 2;
    private const int Pending = 0;
    private const int Signed = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.GetFullPath(Path.Combine(storage.Root, "..", "signing"));
    private readonly IDataProtector otpProtector = protection.CreateProtector("ConTrack.CustomerSigningOtp.v1");

    public async Task<CustomerPortalSigningState> PortalState(long customerId, long reviewId, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ContractReviewRequestId == reviewId && x.CustomerId == customerId, ct)
            ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y há»£p Ä‘á»“ng.");
        if (review.ContractReviewRequestStatus != ContractReviewStatus.Approved)
            return new("WAITING_APPROVAL", null, null);
        var document = await db.ContractDocuments.AsNoTracking().SingleAsync(x => x.ContractDocumentId == review.ContractDocumentId && x.TenantId == null, ct);
        if (document.ContractDocumentCurrentVersionId != review.ContractVersionId)
            return new("SUPERSEDED", null, null);
        var signers = await db.ContractElectronicSignatures.AsNoTracking().Where(x => x.ContractDocumentId == review.ContractDocumentId
            && x.ContractVersionId == review.ContractVersionId).ToListAsync(ct);
        var company = signers.SingleOrDefault(x => x.ContractElectronicPartytype == 1);
        var customer = signers.SingleOrDefault(x => x.ContractElectronicPartytype == CustomerParty);
        if (customer?.ContractElectronicStatus == Signed)
            return new("SIGNED", company?.ContractElectronicSignedat, customer.ContractElectronicSignedat);
        if (company?.ContractElectronicStatus == Signed && customer?.ContractElectronicStatus == Pending
            && document.ContractDocumentStatus == ContractDocumentStatus.PendingSignature)
            return new("READY", company.ContractElectronicSignedat, null);
        return new("WAITING_COMPANY", company?.ContractElectronicSignedat, null);
    }

    public async Task<CustomerSigningOtpResult> RequestPortalOtp(long customerId, long reviewId, CancellationToken ct)
    {
        if (!environment.IsDevelopment() && !smsOptions.Value.Enabled)
            throw new InternalAppException(503, "Dá»‹ch vá»¥ gá»­i OTP chÆ°a Ä‘Æ°á»£c cáº¥u hÃ¬nh.");
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        var now = DateTime.UtcNow;
        if (link.ActivatedAt is DateTime activated && activated > now.AddMinutes(-1))
            throw new InternalAppException(429, "Vui lÃ²ng chá» má»™t phÃºt trÆ°á»›c khi yÃªu cáº§u mÃ£ má»›i.");
        var previous = await db.ContractCustomerOtpChallenges.Where(x => x.ContractAccessLinkId == link.ContractAccessLinkId && x.UsedAt == null).ToListAsync(ct);
        foreach (var challenge in previous) challenge.UsedAt = now;
        link.ActivatedAt = now;
        var challengeId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expires = now.AddMinutes(5);
        db.ContractCustomerOtpChallenges.Add(new ContractCustomerOtpChallenge
        {
            ContractAccessLinkId = link.ContractAccessLinkId,
            ContractElectronicSignatureId = await CustomerSignatureId(link.ContractDocumentId, link.ContractVersionId, ct),
            PublicChallengeId = challengeId, ProtectedOtp = otpProtector.Protect(otp), ExpiresAt = expires, FailedAttemptCount = 0, CreatedAt = now
        });
        var phone = await db.ContractPhoneVerifies.AsNoTracking().SingleAsync(x => x.ContractPhoneVerifyId == link.ContractAccessLinkVerificationPhoneId, ct);
        var document = await db.ContractDocuments.AsNoTracking().SingleAsync(x => x.ContractDocumentId == link.ContractDocumentId, ct);
        if (environment.IsDevelopment())
        {
            logger.LogWarning("DEVELOPMENT ONLY: customer signing OTP {Otp} for customer {CustomerId}, contract {ContractId}, phone ending {PhoneSuffix}",
                otp, document.CustomerId, document.ContractDocumentId,
                phone.ContractPhoneVerifyPhoneNumberNormalized.Length <= 4 ? phone.ContractPhoneVerifyPhoneNumberNormalized : phone.ContractPhoneVerifyPhoneNumberNormalized[^4..]);
        }
        else
        {
            sms.Enqueue(db, SmsPurpose.ElectronicSigningOtp, document.CustomerId, document.ContractDocumentId, phone.ContractPhoneVerifyPhoneNumberNormalized,
                $"ConTrack: Ma OTP ky hop dong cua ban la {otp}. Ma het han sau 5 phut.", now);
        }
        await db.SaveChangesAsync(ct);
        return new CustomerSigningOtpResult(challengeId, expires, environment.IsDevelopment() ? "CONSOLE" : "SMS", environment.IsDevelopment() ? otp : null);
    }

    public async Task VerifyPortalOtp(long customerId, long reviewId, CustomerSigningOtpVerification request, CancellationToken ct)
    {
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        await VerifyOtpForLink(link, request, ct);
    }

    public async Task<byte[]> PortalDocument(long customerId, long reviewId, string challengeId, CancellationToken ct)
    {
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        await RequireVerifiedChallenge(link.ContractAccessLinkId, challengeId, ct);
        return (await BuildPortalSigningPdf(link, customerId, ct)).Content;
    }

    public async Task<byte[]> PortalFinalDocument(long customerId, long reviewId, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ContractReviewRequestId == reviewId && x.CustomerId == customerId, ct)
            ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y há»£p Ä‘á»“ng.");
        var signed = await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == review.ContractDocumentId
            && x.ContractVersionId == review.ContractVersionId && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Signed, ct);
        if (!signed) throw new InternalAppException(409, "KhÃ¡ch hÃ ng chÆ°a kÃ½ xong há»£p Ä‘á»“ng.");
        return await CurrentPdf(review.ContractDocumentId, review.ContractVersionId, ct);
    }

    public async Task<byte[]> PortalSign(long customerId, long reviewId, CustomerSigningRequest request, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        if (!request.Confirmed) throw new ArgumentException("Vui lÃ²ng xÃ¡c nháº­n báº¡n Ä‘Ã£ Ä‘á»c há»£p Ä‘á»“ng vÃ  Ä‘á»“ng Ã½ kÃ½.");
        var png = DecodeSignature(request.SignaturePngBase64);
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        var challenge = await RequireVerifiedChallenge(link.ContractAccessLinkId, request.ChallengeId, ct);
        var signer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractElectronicSignatureId == challenge.ContractElectronicSignatureId
            && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Pending, ct);
        var document = await db.ContractDocuments.SingleAsync(x => x.ContractDocumentId == link.ContractDocumentId && x.CustomerId == customerId && x.TenantId == null, ct);
        var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == link.ContractVersionId, ct);
        if (document.ContractDocumentCurrentVersionId != version.ContractVersionId || !version.ContractVersionIslocked
            || document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature)
            throw new InternalAppException(409, "PhiÃªn báº£n há»£p Ä‘á»“ng Ä‘Ã£ thay Ä‘á»•i hoáº·c khÃ´ng cÃ²n Ä‘Æ°á»£c phÃ©p kÃ½.");
        var prepared = await BuildPortalSigningPdf(link, customerId, ct);
        var source = prepared.Content;
        var placement = new PdfSignaturePlacement(prepared.SignaturePage, prepared.CustomerSignatureX,
            prepared.SignatureY, prepared.SignatureWidth, prepared.SignatureHeight);
        var signed = signatures.StampSignature(source, png, placement);
        var signedKey = $"{link.ContractDocumentId}-{link.ContractVersionId}-final-{Guid.NewGuid():N}.pdf";
        var companySignedLayoutKey = $"{link.ContractDocumentId}-{link.ContractVersionId}-company-layout-{Guid.NewGuid():N}.pdf";
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, signedKey), signed, ct);
        await File.WriteAllBytesAsync(Path.Combine(root, companySignedLayoutKey), source, ct);
        try
        {
            return await InTransaction(async () =>
            {
                var now = DateTime.UtcNow;
                var current = await (from relation in db.ContractDocumentFiles join stored in db.FileStorages on relation.FileStorageFileid equals stored.FileStorageFileid
                    where relation.ContractDocumentId == link.ContractDocumentId && relation.ContractVersionId == link.ContractVersionId && relation.IsPrimary
                    select new { Relation = relation, Storage = stored }).SingleAsync(ct);
                current.Relation.IsPrimary = false;
                signer.ContractElectronicStatus = Signed;
                signer.ContractElectronicSignedat = now;
                signer.ContractElectronicSignatureimagestoragekey = "embedded-in-signed-pdf";
                signer.ContractElectronicIpaddress = Cut(ipAddress, 45);
                signer.ContractElectronicUseragent = Cut(userAgent, 1000);
                signer.ContractElectronicPagenumber = prepared.SignaturePage;
                signer.ContractElectronicPositionx = prepared.CustomerSignatureX;
                signer.ContractElectronicPositiony = prepared.SignatureY;
                signer.ContractElectronicWidth = prepared.SignatureWidth;
                signer.ContractElectronicHeight = prepared.SignatureHeight;
                signer.ContractElectronicDocumentHashAtSigning = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
                challenge.UsedAt = now;
                document.ContractDocumentStatus = ContractDocumentStatus.Signed;
                document.UpdatedDate = now;
                version.ContractVersionSigndate = now;
                link.RevokedAt = now;
                link.ContractAccessLinkRevocationReason = "CUSTOMER_SIGNED";
                ReviewData.Audit(db, "CUSTOMER_SIGNING_PDF_REFORMATTED", "CONTRACT_DOCUMENTS", document.ContractDocumentId, "CUSTOMER", customerId, now);
                var companySignedLayout = new FileStorage
                {
                    FileStorageObjectid = document.ContractDocumentId, FileStorageObjecttype = "CONTRACT_SIGNING",
                    FileStorageFiletype = "COMPANY_SIGNED_PDF_REFORMATTED", FileStorageFilename = (document.ContractDocumentCode ?? $"contract-{document.ContractDocumentId}") + "-company-approved-layout.pdf",
                    FileStorageFilepath = companySignedLayoutKey, FileStorageStoragekey = companySignedLayoutKey, FileStorageContenttype = "application/pdf",
                    FileStorageFilesize = source.Length, FileStorageSha256 = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(),
                    FileStorageUploadedbyemployeeid = document.EmployeeId, FileStorageUploadeddate = now
                };
                db.FileStorages.Add(companySignedLayout);
                await db.SaveChangesAsync(ct);
                db.ContractDocumentFiles.Add(new ContractDocumentFile { ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId,
                    FileStorageFileid = companySignedLayout.FileStorageFileid, ContractDocumentFileRole = 1, IsPrimary = false, CreatedDate = now, CreatedEmployeeId = document.EmployeeId });
                await db.SaveChangesAsync(ct);
                var file = new FileStorage
                {
                    FileStorageObjectid = document.ContractDocumentId, FileStorageObjecttype = "CONTRACT_SIGNING", FileStorageFiletype = "FULLY_SIGNED_PDF",
                    FileStorageFilename = (document.ContractDocumentCode ?? $"contract-{document.ContractDocumentId}") + "-signed.pdf", FileStorageFilepath = signedKey,
                    FileStorageStoragekey = signedKey, FileStorageContenttype = "application/pdf", FileStorageFilesize = signed.Length,
                    FileStorageSha256 = Convert.ToHexString(SHA256.HashData(signed)).ToLowerInvariant(),
                    FileStorageUploadedbyemployeeid = document.EmployeeId, FileStorageUploadeddate = now
                };
                db.FileStorages.Add(file);
                await db.SaveChangesAsync(ct);
                var finalRelation = new ContractDocumentFile { ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId,
                    FileStorageFileid = file.FileStorageFileid, ContractDocumentFileRole = 1, IsPrimary = true, CreatedDate = now, CreatedEmployeeId = document.EmployeeId };
                db.ContractDocumentFiles.Add(finalRelation);
                await db.SaveChangesAsync(ct);
                db.ContractSignedEvidences.Add(new ContractSignedEvidence { ContractDocumentFileId = finalRelation.ContractDocumentFileId,
                    ContractSignedEvidenceStatus = 1, ContractSignedEvidenceSigndate = DateOnly.FromDateTime(now), UploadedByEmployeeid = document.EmployeeId, UploadedAt = now });
                ReviewData.Audit(db, "CUSTOMER_SIGNED", "CONTRACT_ELECTRONIC_SIGNATURE", signer.ContractElectronicSignatureId, "CUSTOMER", customerId, now);
                await db.SaveChangesAsync(ct);
                return signed;
            }, ct);
        }
        catch
        {
            File.Delete(Path.Combine(root, signedKey));
            File.Delete(Path.Combine(root, companySignedLayoutKey));
            throw;
        }
    }

    private async Task<PreparedCustomerSigningPdf> BuildPortalSigningPdf(ContractAccessLink link, long customerId, CancellationToken ct)
    {
        var document = await db.ContractDocuments.AsNoTracking().SingleAsync(x => x.ContractDocumentId == link.ContractDocumentId
            && x.CustomerId == customerId && x.TenantId == null, ct);
        var version = await db.ContractVersions.AsNoTracking().SingleAsync(x => x.ContractVersionId == link.ContractVersionId
            && x.ContractDocumentId == document.ContractDocumentId, ct);
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.CustomerId == document.CustomerId, ct);
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == document.ContractDocumentId
            && x.ContractVersionId == version.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct)
            ?? throw new InternalAppException(409, "Không tìm thấy bản chụp hợp đồng để dựng PDF.");
        if (!SnapshotHash.Compute(snapshot.FieldValue).Equals(version.ContractVersionSnapshothash, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "Bản chụp hợp đồng không khớp hash đã duyệt.");
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)
            ?? throw new InternalAppException(409, "Bản chụp hợp đồng không hợp lệ.");
        var companySigner = await db.ContractElectronicSignatures.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == document.ContractDocumentId
            && x.ContractVersionId == version.ContractVersionId && x.ContractElectronicPartytype == 1 && x.ContractElectronicStatus == Signed, ct)
            ?? throw new InternalAppException(409, "Công ty chưa ký hợp đồng.");
        var companySignedPdf = await CurrentPdf(document.ContractDocumentId, version.ContractVersionId, ct);
        var companySignature = ExtractCompanySignature(companySignedPdf, companySigner.ContractElectronicPagenumber ?? 1);
        var layout = InternalSigningService.RenderContractPdf(document, version, customer, content);
        var companyPlacement = new PdfSignaturePlacement(layout.SignaturePage, layout.CompanySignatureX, layout.SignatureY,
            layout.SignatureWidth, layout.SignatureHeight);
        var companySignedLayout = signatures.StampSignature(layout.Content, companySignature, companyPlacement);
        return new PreparedCustomerSigningPdf(companySignedLayout, layout.SignaturePage, layout.CompanySignatureX, layout.CustomerSignatureX,
            layout.SignatureY, layout.SignatureWidth, layout.SignatureHeight);
    }

    internal static byte[] ExtractCompanySignature(byte[] pdfBytes, int pageNumber)
    {
        using var pdf = PdfDocument.Open(pdfBytes);
        if (pageNumber < 1 || pageNumber > pdf.NumberOfPages)
            throw new InternalAppException(409, "Không tìm thấy trang có chữ ký công ty trong bản PDF đã ký.");
        var images = pdf.GetPage(pageNumber).GetImages();
        var signatures = images.Where(image => image.TryGetPng(out _)).ToList();
        if (signatures.Count != 1 || !signatures[0].TryGetPng(out var png))
            throw new InternalAppException(409, "Không thể lấy chữ ký công ty từ bản PDF hiện tại để dựng lại bố cục.");
        return png;
    }

    private sealed record PreparedCustomerSigningPdf(byte[] Content, int SignaturePage, int CompanySignatureX, int CustomerSignatureX,
        int SignatureY, int SignatureWidth, int SignatureHeight);

    private async Task VerifyOtpForLink(ContractAccessLink link, CustomerSigningOtpVerification request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Otp) || request.Otp.Length != 6 || request.Otp.Any(c => !char.IsAsciiDigit(c)))
            throw new ArgumentException("MÃ£ OTP pháº£i gá»“m 6 chá»¯ sá»‘.");
        var challenge = await db.ContractCustomerOtpChallenges.SingleOrDefaultAsync(x => x.PublicChallengeId == request.ChallengeId
            && x.ContractAccessLinkId == link.ContractAccessLinkId, ct) ?? throw new InternalAppException(400, "MÃ£ xÃ¡c minh khÃ´ng há»£p lá»‡.");
        var now = DateTime.UtcNow;
        if (challenge.UsedAt is not null || challenge.VerifiedAt is not null || challenge.ExpiresAt <= now || challenge.FailedAttemptCount >= 5)
            throw new InternalAppException(400, "MÃ£ xÃ¡c minh Ä‘Ã£ háº¿t háº¡n hoáº·c bá»‹ khÃ³a.");
        var expected = otpProtector.Unprotect(challenge.ProtectedOtp);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(request.Otp)))
        {
            challenge.FailedAttemptCount++;
            await db.SaveChangesAsync(ct);
            throw new InternalAppException(400, "MÃ£ OTP khÃ´ng chÃ­nh xÃ¡c.");
        }
        challenge.VerifiedAt = now;
        var signer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractElectronicSignatureId == challenge.ContractElectronicSignatureId, ct);
        signer.ContractElectronicCustomerotpchallengeid = challenge.ContractCustomerOtpChallengeId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ContractAccessLink> ResolvePortalLink(long customerId, long reviewId, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ContractReviewRequestId == reviewId && x.CustomerId == customerId, ct)
            ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y há»£p Ä‘á»“ng.");
        if (review.ContractReviewRequestStatus != ContractReviewStatus.Approved)
            throw new InternalAppException(409, "KhÃ¡ch hÃ ng cáº§n Ä‘á»“ng Ã½ ná»™i dung trÆ°á»›c khi kÃ½.");
        var link = await db.ContractAccessLinks.SingleOrDefaultAsync(x => x.ContractDocumentId == review.ContractDocumentId && x.ContractVersionId == review.ContractVersionId
            && x.TenantId == null && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow
            && db.ContractDocuments.Any(d => d.ContractDocumentId == x.ContractDocumentId && d.CustomerId == customerId
                && d.ContractDocumentCurrentVersionId == x.ContractVersionId && d.ContractDocumentStatus == ContractDocumentStatus.PendingSignature), ct)
            ?? throw new InternalAppException(409, "Há»£p Ä‘á»“ng chÆ°a Ä‘áº¿n bÆ°á»›c khÃ¡ch hÃ ng kÃ½ hoáº·c link kÃ½ Ä‘Ã£ háº¿t háº¡n.");
        var companySigned = await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == link.ContractDocumentId
            && x.ContractVersionId == link.ContractVersionId && x.ContractElectronicPartytype == 1 && x.ContractElectronicStatus == Signed, ct);
        if (!companySigned) throw new InternalAppException(409, "CÃ´ng ty chÆ°a kÃ½ há»£p Ä‘á»“ng.");
        return link;
    }

    private async Task<ContractCustomerOtpChallenge> RequireVerifiedChallenge(long linkId, string challengeId, CancellationToken ct) =>
        await db.ContractCustomerOtpChallenges.SingleOrDefaultAsync(x => x.ContractAccessLinkId == linkId && x.PublicChallengeId == challengeId
            && x.VerifiedAt != null && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow && x.FailedAttemptCount < 5, ct)
        ?? throw new InternalAppException(401, "Hãy xác minh OTP trước khi xem hoặc ký hợp đồng.");

    private async Task<long> CustomerSignatureId(long documentId, long versionId, CancellationToken ct) => await db.ContractElectronicSignatures
        .Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == versionId && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Pending)
        .Select(x => x.ContractElectronicSignatureId).SingleAsync(ct);

    private async Task<byte[]> CurrentPdf(long documentId, long versionId, CancellationToken ct)
    {
        var file = await (from relation in db.ContractDocumentFiles.AsNoTracking()
            join stored in db.FileStorages.AsNoTracking() on relation.FileStorageFileid equals stored.FileStorageFileid
            where relation.ContractDocumentId == documentId && relation.ContractVersionId == versionId && relation.IsPrimary
            select stored).SingleOrDefaultAsync(ct) ?? throw new InternalAppException(404, "Không tìm thấy bản PDF hợp đồng.");
        if (Path.GetFileName(file.FileStorageStoragekey) != file.FileStorageStoragekey) throw new InvalidDataException("Invalid signing storage key.");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, file.FileStorageStoragekey), ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.FileStorageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "File hợp đồng không khớp mã kiểm tra đã lưu.");
        return bytes;
    }

    private static byte[] DecodeSignature(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 1_400_000) throw new ArgumentException("Ảnh chữ ký PNG không hợp lệ.");
        var comma = input.IndexOf(',');
        try { return Convert.FromBase64String(comma >= 0 ? input[(comma + 1)..] : input); }
        catch (FormatException) { throw new ArgumentException("Ảnh chữ ký PNG không hợp lệ."); }
    }

    private static string? Cut(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    private async Task<T> InTransaction<T>(Func<Task<T>> action, CancellationToken ct) => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    });
}
