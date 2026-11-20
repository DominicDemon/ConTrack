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
    IOptions<EsmsOptions> smsOptions,
    CustomerContractMailer? customerMail = null) : ICustomerSigningService
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
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
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
            throw new InternalAppException(503, "Dịch vụ gửi OTP chưa được cấu hình.");
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        var now = DateTime.UtcNow;
        // Opening the review link also sets ActivatedAt. Only a previous signing OTP starts the resend cooldown.
        var cooldownStart = now.AddMinutes(-1);
        if (await db.ContractCustomerOtpChallenges.AnyAsync(x => x.ContractAccessLinkId == link.ContractAccessLinkId
            && x.CreatedAt > cooldownStart, ct))
            throw new InternalAppException(429, "Vui lòng chờ một phút trước khi yêu cầu mã mới.");
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
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        var signed = await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == review.ContractDocumentId
            && x.ContractVersionId == review.ContractVersionId && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Signed, ct);
        if (!signed) throw new InternalAppException(409, "Khách hàng chưa ký xong hợp đồng.");
        return await CurrentPdf(review.ContractDocumentId, review.ContractVersionId, ct, finalOnly: true);
    }

    private IQueryable<ContractVersion> VisibleSignedVersions(InternalUser user)
    {
        bool companyWide = user.CanCheck || user.CanApprove;
        if (!companyWide && !user.CanWrite) throw new InternalAppException(403, "Bạn không có quyền xem hợp đồng đã ký.");
        var signatures = db.ContractElectronicSignatures.Where(s => s.ContractElectronicStatus == Signed);
        return from v in db.ContractVersions.AsNoTracking()
               join d in db.ContractDocuments on v.ContractDocumentId equals d.ContractDocumentId
               where d.TenantId == null && (companyWide || d.EmployeeId == user.Id)
                   && signatures.Any(s => s.ContractDocumentId == d.ContractDocumentId && s.ContractVersionId == v.ContractVersionId && s.ContractElectronicPartytype == 1)
                   && signatures.Any(s => s.ContractDocumentId == d.ContractDocumentId && s.ContractVersionId == v.ContractVersionId && s.ContractElectronicPartytype == CustomerParty)
               select v;
    }

    public async Task<SignedContractPage> SignedContracts(InternalUser user, string? search, int page, CancellationToken ct)
    {
        const int size = 20;
        if (page < 1 || page > int.MaxValue / size) throw new InternalAppException(400, "Số trang không hợp lệ.");
        var term = search?.Trim();
        var query = from v in VisibleSignedVersions(user)
                    join d in db.ContractDocuments on v.ContractDocumentId equals d.ContractDocumentId
                    join c in db.Customers on d.CustomerId equals c.CustomerId
                    join e in db.Employees on d.EmployeeId equals e.EmployeeId
                    where string.IsNullOrEmpty(term) || d.ContractDocumentName.Contains(term)
                        || (d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(term)) || c.CustomerDisplayName.Contains(term)
                    let signedAt = v.ContractVersionSigndate ?? db.ContractElectronicSignatures
                        .Where(s => s.ContractVersionId == v.ContractVersionId && s.ContractElectronicStatus == Signed)
                        .Max(s => s.ContractElectronicSignedat)
                    orderby signedAt descending, v.ContractVersionId descending
                    select new SignedContractEntry(d.ContractDocumentId, v.ContractVersionId, d.ContractDocumentCode ?? "",
                        d.ContractDocumentName, c.CustomerDisplayName, e.EmployeeDisplayName, v.ContractVersionVersionNo, signedAt);
        var rows = await query.Skip((page - 1) * size).Take(size + 1).ToListAsync(ct);
        return new(rows.Take(size).ToList(), rows.Count > size);
    }

    public async Task<byte[]> InternalFinalDocument(InternalUser user, long documentId, long versionId, CancellationToken ct)
    {
        if (!await VisibleSignedVersions(user).AnyAsync(v => v.ContractDocumentId == documentId && v.ContractVersionId == versionId, ct))
            throw new InternalAppException(404, "Không tìm thấy hợp đồng đã ký.");
        return await CurrentPdf(documentId, versionId, ct, finalOnly: true);
    }

    public async Task<byte[]> PortalSign(long customerId, long reviewId, CustomerSigningRequest request, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        if (!request.Confirmed) throw new ArgumentException("Vui lòng xác nhận bạn đã đọc hợp đồng và đồng ý ký.");
        var png = DecodeSignature(request.SignaturePngBase64);
        var link = await ResolvePortalLink(customerId, reviewId, ct);
        var challenge = await RequireVerifiedChallenge(link.ContractAccessLinkId, request.ChallengeId, ct);
        var signer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractElectronicSignatureId == challenge.ContractElectronicSignatureId
            && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Pending, ct);
        var document = await db.ContractDocuments.SingleAsync(x => x.ContractDocumentId == link.ContractDocumentId && x.CustomerId == customerId && x.TenantId == null, ct);
        var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == link.ContractVersionId, ct);
        if (document.ContractDocumentCurrentVersionId != version.ContractVersionId || !version.ContractVersionIslocked
            || document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature)
            throw new InternalAppException(409, "Phiên bản hợp đồng đã thay đổi hoặc không còn được phép ký.");
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
            var result = await InTransaction(async () =>
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
                // The customer signs last, so both sides have now signed: the Sale in charge and Hành chính are told at once,
                // and a prospect (no portal account yet) is flagged so they create the account.
                var prospect = !await ReviewData.HasAccount(db, customerId, ct);
                await ReviewData.Notify(db, document, $"customer-signed:{document.ContractDocumentId}:{version.ContractVersionId}", "CUSTOMER_SIGNED",
                    prospect ? "Hai bên đã ký · tạo tài khoản cho khách" : "Hai bên đã ký hợp đồng",
                    $"{document.ContractDocumentCode} · {document.ContractDocumentName}" + (prospect ? " · Khách tiềm năng chưa có tài khoản: mở hợp đồng và bấm “Tạo tài khoản khách”." : ""),
                    await ReviewData.WithPermission(db, "contract.check", ct), now, ct);
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
            // After the commit: confirm by email that the contract is signed by both sides (the portal shows the final PDF).
            if (customerMail is not null)
                await customerMail.Send(link.ContractDocumentId, "Hợp đồng đã ký xong",
                    "Bạn và công ty đã ký đủ hợp đồng. Bạn có thể tải bản PDF đã ký trên Cổng khách hàng.", ct,
                    "Bạn và công ty đã ký đủ hợp đồng. Nhân viên phụ trách sẽ gửi tài khoản Cổng khách hàng qua email để bạn xem và tải bản đã ký.");
            return result;
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
            throw new ArgumentException("Mã OTP phải gồm 6 chữ số.");
        var challenge = await db.ContractCustomerOtpChallenges.SingleOrDefaultAsync(x => x.PublicChallengeId == request.ChallengeId
            && x.ContractAccessLinkId == link.ContractAccessLinkId, ct) ?? throw new InternalAppException(400, "Mã xác minh không hợp lệ.");
        var now = DateTime.UtcNow;
        if (challenge.UsedAt is not null || challenge.VerifiedAt is not null || challenge.ExpiresAt <= now || challenge.FailedAttemptCount >= 5)
            throw new InternalAppException(400, "Mã xác minh đã hết hạn hoặc bị khóa.");
        var expected = otpProtector.Unprotect(challenge.ProtectedOtp);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(request.Otp)))
        {
            challenge.FailedAttemptCount++;
            await db.SaveChangesAsync(ct);
            throw new InternalAppException(400, "Mã OTP không chính xác.");
        }
        challenge.VerifiedAt = now;
        var signer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractElectronicSignatureId == challenge.ContractElectronicSignatureId, ct);
        signer.ContractElectronicCustomerotpchallengeid = challenge.ContractCustomerOtpChallengeId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ContractAccessLink> ResolvePortalLink(long customerId, long reviewId, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ContractReviewRequestId == reviewId && x.CustomerId == customerId, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        if (review.ContractReviewRequestStatus != ContractReviewStatus.Approved)
            throw new InternalAppException(409, "Khách hàng cần đồng ý nội dung trước khi ký.");
        var link = await db.ContractAccessLinks.SingleOrDefaultAsync(x => x.ContractDocumentId == review.ContractDocumentId && x.ContractVersionId == review.ContractVersionId
            && x.TenantId == null && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow
            && db.ContractDocuments.Any(d => d.ContractDocumentId == x.ContractDocumentId && d.CustomerId == customerId
                && d.ContractDocumentCurrentVersionId == x.ContractVersionId && d.ContractDocumentStatus == ContractDocumentStatus.PendingSignature), ct)
            ?? throw new InternalAppException(409, "Hợp đồng chưa đến bước khách hàng ký hoặc link ký đã hết hạn.");
        var companySigned = await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == link.ContractDocumentId
            && x.ContractVersionId == link.ContractVersionId && x.ContractElectronicPartytype == 1 && x.ContractElectronicStatus == Signed, ct);
        if (!companySigned) throw new InternalAppException(409, "Công ty chưa ký hợp đồng.");
        return link;
    }

    private async Task<ContractCustomerOtpChallenge> RequireVerifiedChallenge(long linkId, string challengeId, CancellationToken ct) =>
        await db.ContractCustomerOtpChallenges.SingleOrDefaultAsync(x => x.ContractAccessLinkId == linkId && x.PublicChallengeId == challengeId
            && x.VerifiedAt != null && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow && x.FailedAttemptCount < 5, ct)
        ?? throw new InternalAppException(401, "Hãy xác minh OTP trước khi xem hoặc ký hợp đồng.");

    private async Task<long> CustomerSignatureId(long documentId, long versionId, CancellationToken ct) => await db.ContractElectronicSignatures
        .Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == versionId && x.ContractElectronicPartytype == CustomerParty && x.ContractElectronicStatus == Pending)
        .Select(x => x.ContractElectronicSignatureId).SingleAsync(ct);

    private async Task<byte[]> CurrentPdf(long documentId, long versionId, CancellationToken ct, bool finalOnly = false)
    {
        var file = await (from relation in db.ContractDocumentFiles.AsNoTracking()
            join stored in db.FileStorages.AsNoTracking() on relation.FileStorageFileid equals stored.FileStorageFileid
            where relation.ContractDocumentId == documentId && relation.ContractVersionId == versionId && relation.IsPrimary
                && (!finalOnly || stored.FileStorageFiletype == "FULLY_SIGNED_PDF")
            select stored).SingleOrDefaultAsync(ct) ?? throw new InternalAppException(404, "Không tìm thấy bản PDF hợp đồng.");
        if (Path.GetFileName(file.FileStorageStoragekey) != file.FileStorageStoragekey) throw new InvalidDataException("Invalid signing storage key.");
        var path = Path.Combine(root, file.FileStorageStoragekey);
        if (!File.Exists(path)) throw new InternalAppException(404, "Không tìm thấy tệp PDF đã lưu của phiên bản này.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
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
