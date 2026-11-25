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
using ContractDocumentFileRoles = ContractManagement.Domain.Internal.Scans.ContractDocumentFileRoles;
using SignedEvidenceStatus = ContractManagement.Domain.Internal.Scans.SignedEvidenceStatus;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class CustomerSigningService(
    MasterDbContext db,
    InternalTemplateStorage storage,
    IElectronicSigningPdfService signatures,
    ICustomerOtpMail otpMail,
    IDataProtectionProvider protection,
    IHostEnvironment environment,
    ILogger<CustomerSigningService> logger,
    CustomerContractMailer? customerMail = null) : ICustomerSigningService
{
    private const int CustomerParty = 2;
    private const int Pending = 0;
    private const int Signed = 1;
    private const int Paid = 1;
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
        if (environment.IsDevelopment())
            logger.LogWarning("DEVELOPMENT ONLY: customer signing OTP {Otp} for contract {ContractId}", otp, link.ContractDocumentId);
        // Send before saving: a code that never reached the customer must not start the one-minute cooldown.
        await otpMail.Send(link.ContractDocumentId, otp, expires, ct);
        await db.SaveChangesAsync(ct);
        return new CustomerSigningOtpResult(challengeId, expires, "EMAIL", environment.IsDevelopment() ? otp : null);
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
        return await BuildPortalSigningPdf(link, customerId, ct);
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
        return from v in CompletedVersions(db)
               join d in db.ContractDocuments on v.ContractDocumentId equals d.ContractDocumentId
               where d.TenantId == null && (companyWide || d.EmployeeId == user.Id)
               select v;
    }

    internal static IQueryable<ContractVersion> CompletedVersions(MasterDbContext db)
    {
        var signatures = db.ContractElectronicSignatures.Where(s => s.ContractElectronicStatus == Signed);
        var paper = AcceptedPaperFiles(db);
        return db.ContractVersions.AsNoTracking().Where(v =>
            (signatures.Any(s => s.ContractDocumentId == v.ContractDocumentId && s.ContractVersionId == v.ContractVersionId && s.ContractElectronicPartytype == 1)
             && signatures.Any(s => s.ContractDocumentId == v.ContractDocumentId && s.ContractVersionId == v.ContractVersionId && s.ContractElectronicPartytype == CustomerParty))
            || paper.Any(f => f.ContractDocumentId == v.ContractDocumentId && f.ContractVersionId == v.ContractVersionId));
    }

    private static IQueryable<ContractDocumentFile> AcceptedPaperFiles(MasterDbContext db) =>
        from file in db.ContractDocumentFiles.AsNoTracking()
        join evidence in db.ContractSignedEvidences on file.ContractDocumentFileId equals evidence.ContractDocumentFileId
        where file.ContractDocumentFileRole == ContractDocumentFileRoles.SignedPaper
           && evidence.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.Accepted
        select file;

    public async Task<SignedContractPage> SignedContracts(InternalUser user, string? search, int page, CancellationToken ct, long? documentId = null)
    {
        const int size = 10;
        if (page < 1 || page > int.MaxValue / size) throw new InternalAppException(400, "Số trang không hợp lệ.");
        var term = search?.Trim();
        var paper = AcceptedPaperFiles(db);
        var query = from v in VisibleSignedVersions(user)
                    join d in db.ContractDocuments on v.ContractDocumentId equals d.ContractDocumentId
                    join c in db.Customers on d.CustomerId equals c.CustomerId
                    join e in db.Employees on d.EmployeeId equals e.EmployeeId
                    where (!documentId.HasValue || d.ContractDocumentId == documentId) && (string.IsNullOrEmpty(term) || d.ContractDocumentName.Contains(term)
                        || (d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(term)) || c.CustomerDisplayName.Contains(term))
                    let signedAt = v.ContractVersionSigndate ?? db.ContractElectronicSignatures
                        .Where(s => s.ContractVersionId == v.ContractVersionId && s.ContractElectronicStatus == Signed)
                        .Max(s => s.ContractElectronicSignedat)
                    orderby signedAt descending, v.ContractVersionId descending
                    select new SignedContractEntry(d.ContractDocumentId, v.ContractVersionId, d.ContractDocumentCode ?? "",
                        d.ContractDocumentName, c.CustomerDisplayName, e.EmployeeDisplayName, v.ContractVersionVersionNo, signedAt)
                    {
                        PaperSigned = paper.Any(f => f.ContractDocumentId == d.ContractDocumentId && f.ContractVersionId == v.ContractVersionId)
                    };
        var rows = await query.Skip((page - 1) * size).Take(size + 1).ToListAsync(ct);
        var items = rows.Take(size).ToList();
        if (items.Count == 0) return new(items, false);
        var versions = items.Select(row => row.VersionId).ToArray();
        var installments = (await db.ContractPaymentMilestones.AsNoTracking()
            .Where(m => versions.Contains(m.ContractVersionId))
            .OrderBy(m => m.ContractPaymentMilestoneDisplayorder).ThenBy(m => m.ContractPaymentMilestoneId)
            .Select(m => new { m.ContractVersionId, m.ContractDocumentId, Paid = m.ContractPaymentMilestonePaymentstatus == Paid })
            .ToListAsync(ct)).ToLookup(m => (m.ContractDocumentId, m.ContractVersionId));
        return new(items.Select(row =>
        {
            var flags = installments[(row.DocumentId, row.VersionId)].Select(m => m.Paid).ToArray();
            var paid = flags.Count(value => value);
            return row with { Payment = new(flags.Length, paid, flags.Length == 2 && paid == 1 ? Array.IndexOf(flags, true) + 1 : null) };
        }).ToList(), rows.Count > size);
    }

    private async Task RequireSignedVersion(InternalUser user, long documentId, long versionId, CancellationToken ct)
    {
        if (!await VisibleSignedVersions(user).AnyAsync(v => v.ContractDocumentId == documentId && v.ContractVersionId == versionId, ct))
            throw new InternalAppException(404, "Không tìm thấy hợp đồng đã ký.");
    }

    private IQueryable<ContractPaymentMilestone> PaymentSchedule(long documentId, long versionId) =>
        db.ContractPaymentMilestones.Where(m => m.ContractDocumentId == documentId && m.ContractVersionId == versionId);

    public async Task<List<SignedPaymentInstallment>> SignedPayments(InternalUser user, long documentId, long versionId, CancellationToken ct)
    {
        await RequireSignedVersion(user, documentId, versionId, ct);
        return await PaymentSchedule(documentId, versionId).AsNoTracking().OrderBy(m => m.ContractPaymentMilestoneDisplayorder)
            .ThenBy(m => m.ContractPaymentMilestoneId).Select(m => new SignedPaymentInstallment(m.ContractPaymentMilestoneId,
                m.ContractPaymentMilestoneTitlevi, m.ContractPaymentMilestoneAmount, m.ContractPaymentMilestoneDuedate,
                m.ContractPaymentMilestonePaymentstatus == Paid, m.ContractPaymentMilestonePaidat)).ToListAsync(ct);
    }

    public async Task ConfirmSignedPayment(InternalUser user, long documentId, long versionId, long installmentId, CancellationToken ct)
    {
        if (!user.CanCheck) throw new InternalAppException(403, "Chỉ Hành chính được xác nhận thanh toán.");
        await ReviewData.InTransaction(db, async () =>
        {
            await RequireSignedVersion(user, documentId, versionId, ct);
            var installment = PaymentSchedule(documentId, versionId).Where(m => m.ContractPaymentMilestoneId == installmentId);
            var title = await installment.Select(m => m.ContractPaymentMilestoneTitlevi).SingleOrDefaultAsync(ct)
                ?? throw new InternalAppException(404, "Không tìm thấy đợt thanh toán của phiên bản này.");
            var now = DateTime.UtcNow;
            var changed = await installment.Where(m => m.ContractPaymentMilestonePaymentstatus != Paid).ExecuteUpdateAsync(update => update
                .SetProperty(m => m.ContractPaymentMilestonePaymentstatus, Paid)
                .SetProperty(m => m.ContractPaymentMilestonePaidat, now)
                .SetProperty(m => m.ContractPaymentMilestonePaidbyemployeeid, checked((int)user.Id))
                .SetProperty(m => m.UpdatedAt, now), ct);
            if (changed == 0) return false;
            var document = await db.ContractDocuments.AsNoTracking().SingleAsync(d => d.ContractDocumentId == documentId, ct);
            ReviewData.Audit(db, "CONFIRM_PAYMENT", "CONTRACT_PAYMENT_MILESTONE", installmentId, "EMPLOYEE", user.Id, now);
            await ReviewData.Notify(db, document, $"signed-payment:{installmentId}", "SIGNED_PAYMENT_CONFIRMED", "Đã xác nhận thanh toán",
                $"{document.ContractDocumentCode} · {document.ContractDocumentName} · {title}", [], now, ct);
            return true;
        }, ct);
    }

    public async Task<byte[]> InternalFinalDocument(InternalUser user, long documentId, long versionId, CancellationToken ct)
    {
        await RequireSignedVersion(user, documentId, versionId, ct);
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
        var source = await BuildPortalSigningPdf(link, customerId, ct);
        var placement = new PdfSignaturePlacement(signer.ContractElectronicPagenumber ?? 1,
            signer.ContractElectronicPositionx ?? 50, signer.ContractElectronicPositiony ?? 680,
            signer.ContractElectronicWidth ?? 190, signer.ContractElectronicHeight ?? 54);
        var signed = signatures.StampSignature(source, png, placement);
        var signedKey = $"{link.ContractDocumentId}-{link.ContractVersionId}-final-{Guid.NewGuid():N}.pdf";
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, signedKey), signed, ct);
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
                signer.ContractElectronicDocumentHashAtSigning = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
                challenge.UsedAt = now;
                document.ContractDocumentStatus = ContractDocumentStatus.Signed;
                document.UpdatedDate = now;
                version.ContractVersionSigndate = now;
                link.RevokedAt = now;
                link.ContractAccessLinkRevocationReason = "CUSTOMER_SIGNED";
                ReviewData.Audit(db, "CUSTOMER_SIGNED_PDF", "CONTRACT_DOCUMENTS", document.ContractDocumentId, "CUSTOMER", customerId, now);
                // The customer signs last, so both sides have now signed: the Sale in charge and Hành chính are told at once,
                // and a prospect (no portal account yet) is flagged so they create the account.
                var prospect = !await ReviewData.HasAccount(db, customerId, ct);
                await ReviewData.Notify(db, document, $"customer-signed:{document.ContractDocumentId}:{version.ContractVersionId}", "CUSTOMER_SIGNED",
                    prospect ? "Hai bên đã ký · tạo tài khoản cho khách" : "Hai bên đã ký hợp đồng",
                    $"{document.ContractDocumentCode} · {document.ContractDocumentName}" + (prospect ? " · Khách tiềm năng chưa có tài khoản: mở hợp đồng và bấm “Tạo tài khoản khách”." : ""),
                    await ReviewData.WithPermission(db, "contract.check", ct), now, ct);
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
            throw;
        }
    }

    private async Task<byte[]> BuildPortalSigningPdf(ContractAccessLink link, long customerId, CancellationToken ct)
    {
        var document = await db.ContractDocuments.AsNoTracking().SingleAsync(x => x.ContractDocumentId == link.ContractDocumentId
            && x.CustomerId == customerId && x.TenantId == null, ct);
        var version = await db.ContractVersions.AsNoTracking().SingleAsync(x => x.ContractVersionId == link.ContractVersionId
            && x.ContractDocumentId == document.ContractDocumentId, ct);
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == document.ContractDocumentId
            && x.ContractVersionId == version.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct)
            ?? throw new InternalAppException(409, "Không tìm thấy bản chụp hợp đồng để dựng PDF.");
        if (!SnapshotHash.Compute(snapshot.FieldValue).Equals(version.ContractVersionSnapshothash, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "Bản chụp hợp đồng không khớp hash đã duyệt.");
        _ = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)
            ?? throw new InternalAppException(409, "Bản chụp hợp đồng không hợp lệ.");
        var companySigner = await db.ContractElectronicSignatures.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == document.ContractDocumentId
            && x.ContractVersionId == version.ContractVersionId && x.ContractElectronicPartytype == 1 && x.ContractElectronicStatus == Signed, ct)
            ?? throw new InternalAppException(409, "Công ty chưa ký hợp đồng.");
        return await CurrentPdf(document.ContractDocumentId, version.ContractVersionId, ct);
    }

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

    /// <summary>A drawn signature must really be a PNG of at most 1 MB (UploadedImages).</summary>
    private static byte[] DecodeSignature(string input) =>
        ContractManagement.Application.Common.Files.UploadedImages.SignaturePng(input, "Ảnh chữ ký PNG không hợp lệ.");

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
