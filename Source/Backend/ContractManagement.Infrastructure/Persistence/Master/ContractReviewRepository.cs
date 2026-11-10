using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class ContractReviewRepository(MasterDbContext db, IInternalRepository drafts, SmsOutboxWriter sms, ReviewLinkMailer mailer, IConfiguration config) : IContractReviewRepository
{
    public const string InternalCheckWorkflow = "CONTRACT_INTERNAL_CHECK";
    private string PortalUrl => config["CustomerPortal:BaseUrl"] ?? "http://localhost:3000/portal";

    public async Task<ReviewSummary> SendReview(long documentId, SendReviewRequest request, InternalUser actor, CancellationToken ct)
    {
        var mail = await ReviewData.InTransaction(db, async () =>
    {
        var expires = ReviewLink.Expiry(request.LinkExpiresAt, DateTime.UtcNow) ?? throw new ArgumentException("Chọn thời gian hết hạn link sau thời điểm hiện tại.");
        var doc = await OwnedDocument(documentId, actor, ct);
        ReviewData.Expect(doc.RowVersion, request.RowVersion);
        var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
        ReviewData.Expect(version.ContractVersionRowversion, request.VersionRowVersion);
        var latest = await Latest(documentId, ct);
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleAsync(x => x.ContractDocumentId == documentId
            && x.ContractVersionId == version.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
        if (!version.ContractVersionIslocked) {
            if (content.FormatVersion < 2) throw new InternalAppException(422, "Mở nháp, bổ sung pháp nhân/lịch thanh toán và lưu lại trước khi gửi.");
            DraftValidation.Prepare(content, content.TemplateSnapshot ?? throw new InternalAppException(409, "Thiếu bản chụp mẫu."));
            if (content.MissingFields.Count > 0) throw new DraftValidationException(DraftValidation.Missing(content));
            if (content.CustomerBusinessId is { } business && !await db.CustomerBusinesses.AnyAsync(b => b.CustomerId == doc.CustomerId && b.CustomerBusinessId == business, ct)) throw new InternalAppException(422, "Pháp nhân không thuộc khách hàng.");
            if (content.TemplateSnapshot.IsImported && !request.ContentConfirmed) throw new InternalAppException(422, "Cần xác nhận đã kiểm tra nội dung mẫu tải lên.");
        }
        if (ContractReviewPolicy.CanSend(doc.ContractDocumentStatus, latest?.ContractReviewRequestStatus, version.ContractVersionIslocked, content.MissingFields.Count) is { } error)
            throw new InternalAppException(409, error);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == doc.CustomerId && c.CustomerStatus == 1, ct)
            ?? throw new InternalAppException(409, "Khách hàng không còn hoạt động.");
        var phone = ReviewLink.ContactPhone(content.Fields, customer.CustomerPersonalPhone) ?? throw new InternalAppException(409, "Khách hàng chưa có số điện thoại hợp lệ.");
        var email = ReviewLink.ContactEmail(content.Fields, customer.CustomerEmail) ?? throw new InternalAppException(409, "Hợp đồng chưa có email khách hàng hợp lệ để gửi link.");
        var now = DateTime.UtcNow;
        version.ContractVersionIslocked = true; version.ContractVersionLockeddate = now;
        version.ContractVersionLockedbyemployeeid = checked((int)actor.Id); version.ContractVersionSnapshothash = SnapshotHash.Compute(snapshot.FieldValue);
        // REJECTED rows keep their status: CK_ReviewRequest_Followup only allows a follow-up decision on REJECTED.
        if (latest?.ContractReviewRequestStatus == ContractReviewStatus.ChangesRequested) { latest.ContractReviewRequestStatus = ContractReviewStatus.Superseded; latest.UpdatedAt = now; }
        var message = InternalDraftPolicy.Text(request.Message, 1000);
        var review = new ContractReviewRequest
        {
            ContractDocumentId = documentId, ContractVersionId = version.ContractVersionId, ContractReviewRequestDocumentHash = version.ContractVersionSnapshothash,
            PreviousReviewRequestId = latest?.ContractReviewRequestId, CustomerId = doc.CustomerId, ContractReviewRequestMessage = message.Length == 0 ? null : message,
            SentByEmployeeId = actor.Id, SentAt = now, ContractReviewRequestStatus = ContractReviewStatus.Sent, CreatedAt = now
        };
        db.ContractReviewRequests.Add(review);
        doc.ContractDocumentStatus = ContractDocumentStatus.Negotiating; doc.UpdatedDate = now;
        await db.SaveChangesAsync(ct);
        foreach (var comment in await db.ContractNegotiationComments.Where(c => c.ContractDocumentId == documentId && c.ContractVersionId == version.ContractVersionId && c.ContractReviewRequestId == null).ToListAsync(ct))
            comment.ContractReviewRequestId = review.ContractReviewRequestId;
        ReviewData.Audit(db, "SEND_REVIEW", "CONTRACT_REVIEW_REQUEST", review.ContractReviewRequestId, "EMPLOYEE", actor.Id, now);
        var token = await NewLink(documentId, version.ContractVersionId, phone, actor.Id, expires, now, ct);
        await db.SaveChangesAsync(ct);
        return new LinkMail(review.ContractReviewRequestId, email, customer.CustomerDisplayName, doc.ContractDocumentName, token, expires, review.ContractReviewRequestMessage);
    }, ct);
        var sent = await mailer.Send(mail, PortalUrl, ct);
        return (await Summaries(documentId, ct)).First() with { LinkEmailSent = sent };
    }

    /// <summary>Replaces the link of an open review: for a lost or failed email, or to give the customer a new deadline. The old link stops working.</summary>
    public async Task<ReviewSummary> ResendLink(long reviewId, ResendLinkRequest request, InternalUser actor, CancellationToken ct)
    {
        var mail = await ReviewData.InTransaction(db, async () =>
        {
            var now = DateTime.UtcNow;
            var expires = ReviewLink.Expiry(request.LinkExpiresAt, now) ?? throw new ArgumentException("Chọn thời gian hết hạn link sau thời điểm hiện tại.");
            var review = await db.ContractReviewRequests.SingleOrDefaultAsync(r => r.ContractReviewRequestId == reviewId, ct) ?? throw new InternalAppException(404, "Không tìm thấy lần gửi.");
            var doc = await OwnedDocument(review.ContractDocumentId, actor, ct);
            ReviewData.Expect(review.Rowversion, request.RowVersion);
            if (ContractReviewPolicy.CanRespond(review.ContractReviewRequestStatus) is { } error) throw new InternalAppException(409, error);
            var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleAsync(x => x.ContractDocumentId == doc.ContractDocumentId
                && x.ContractVersionId == review.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
            var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
            var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == review.CustomerId, ct);
            var email = ReviewLink.ContactEmail(content.Fields, customer.CustomerEmail) ?? throw new InternalAppException(409, "Hợp đồng chưa có email khách hàng hợp lệ để gửi link.");
            var phone = ReviewLink.ContactPhone(content.Fields, customer.CustomerPersonalPhone) ?? throw new InternalAppException(409, "Khách hàng chưa có số điện thoại hợp lệ.");
            foreach (var old in await db.ContractAccessLinks.Where(l => l.ContractDocumentId == doc.ContractDocumentId && l.ContractVersionId == review.ContractVersionId && l.RevokedAt == null).ToListAsync(ct))
            { old.RevokedAt = now; old.RevokedByEmployeeId = actor.Id; old.ContractAccessLinkRevocationReason = "Thay bằng link mới"; }
            var token = await NewLink(doc.ContractDocumentId, review.ContractVersionId, phone, actor.Id, expires, now, ct);
            review.UpdatedAt = now;
            ReviewData.Audit(db, "RESEND_REVIEW_LINK", "CONTRACT_REVIEW_REQUEST", reviewId, "EMPLOYEE", actor.Id, now);
            await db.SaveChangesAsync(ct);
            return new LinkMail(reviewId, email, customer.CustomerDisplayName, doc.ContractDocumentName, token, expires, review.ContractReviewRequestMessage);
        }, ct);
        // After commit on purpose: a failed email leaves the review open and the sale resends from the panel.
        var sent = await mailer.Send(mail, PortalUrl, ct);
        var documentId = await db.ContractReviewRequests.Where(r => r.ContractReviewRequestId == reviewId).Select(r => r.ContractDocumentId).SingleAsync(ct);
        return (await Summaries(documentId, ct)).Single(r => r.Id == reviewId) with { LinkEmailSent = sent };
    }

    /// <returns>The raw token, which exists only here and in the email. The phone is frozen with the link.</returns>
    private async Task<string> NewLink(long documentId, long versionId, string phone, long actor, DateTime expires, DateTime now, CancellationToken ct)
    {
        var verify = new ContractPhoneVerify { ContractDocumentId = documentId, ContractPhoneVerifyPhoneSource = "CONTRACT", ContractPhoneVerifyPhoneNumberNormalized = phone,
            ContractPhoneVerifyReason = "Xác nhận khách xem hợp đồng qua link email", CreatedByEmployeeid = actor, CreatedAt = now };
        db.ContractPhoneVerifies.Add(verify);
        await db.SaveChangesAsync(ct);
        var token = ReviewLink.NewToken();
        db.ContractAccessLinks.Add(new ContractAccessLink { ContractDocumentId = documentId, ContractVersionId = versionId, ContractAccessLinkVerificationPhoneId = verify.ContractPhoneVerifyId,
            ContractAccessLinkTokenhash = ReviewLink.Hash(token), CreatedByEmployeeId = actor, CreatedAt = now, ExpiresAt = expires });
        return token;
    }

    public async Task<List<ReviewSummary>> Reviews(long documentId, InternalUser actor, CancellationToken ct)
    {
        await OwnedDocument(documentId, actor, ct);
        return await Summaries(documentId, ct);
    }

    public async Task<DraftRecord> CreateVersion(long documentId, NewVersionRequest request, InternalUser actor, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            var doc = await OwnedDocument(documentId, actor, ct);
            ReviewData.Expect(doc.RowVersion, request.RowVersion);
            var current = await db.ContractVersions.AsNoTracking().SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
            var latest = await Latest(documentId, ct);
            if (ContractReviewPolicy.CanCreateVersion(doc.ContractDocumentStatus, latest?.ContractReviewRequestStatus, latest?.ContractReviewRequestFollowupDecision, current.ContractVersionIslocked) is { } error)
                throw new InternalAppException(409, error);
            var now = DateTime.UtcNow;
            var note = InternalDraftPolicy.Text(request.ChangeNote, 1000);
            var next = new ContractVersion
            {
                ContractDocumentId = documentId, ContractTemplateId = current.ContractTemplateId,
                ContractVersionVersionNo = await db.ContractVersions.Where(v => v.ContractDocumentId == documentId).MaxAsync(v => v.ContractVersionVersionNo, ct) + 1,
                ContractVersionChangenote = note.Length == 0 ? "Phiên bản mới sau phản hồi của khách" : note,
                ContractVersionSubtotal = current.ContractVersionSubtotal, ContractVersionTotaldiscount = current.ContractVersionTotaldiscount,
                ContractVersionTotalvat = current.ContractVersionTotalvat, ContractVersionTotalamount = current.ContractVersionTotalamount,
                ContractVersionCurrencycode = current.ContractVersionCurrencycode, ContractVersionEffectiveDate = current.ContractVersionEffectiveDate,
                ContractVersionExpireDate = current.ContractVersionExpireDate, ContractVersionCreatedDate = now
            };
            db.ContractVersions.Add(next);
            await db.SaveChangesAsync(ct);
            var from = current.ContractVersionId;
            foreach (var x in await db.ContractDocumentItems.AsNoTracking().Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == from).ToListAsync(ct))
            { x.ContractDocumentItemId = 0; x.ContractVersionId = next.ContractVersionId; db.ContractDocumentItems.Add(x); }
            var termMap = new Dictionary<long, ContractTerm>();
            foreach (var x in await db.ContractTerms.AsNoTracking().Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == from).ToListAsync(ct))
            { var oldId = x.ContractTermTermid; x.ContractTermTermid = 0; x.ContractVersionId = next.ContractVersionId; x.CreatedAt = now; x.UpdatedAt = null; db.ContractTerms.Add(x); termMap[oldId] = x; }
            await db.SaveChangesAsync(ct);
            foreach (var x in await db.ContractPaymentMilestones.AsNoTracking().Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == from).ToListAsync(ct))
            {
                x.ContractPaymentMilestoneId = 0; x.ContractVersionId = next.ContractVersionId; x.ContractTermId = termMap[x.ContractTermId].ContractTermTermid;
                x.InvoiceId = null; x.ContractPaymentMilestonePaymentstatus = 0; x.ContractPaymentMilestonePaidat = null; x.ContractPaymentMilestonePaidbyemployeeid = null;
                x.ContractPaymentMilestoneAnchordate = null; if (x.ContractPaymentMilestoneDueanchor != 3) x.ContractPaymentMilestoneDuedate = null;
                x.CreatedAt = now; x.UpdatedAt = null; db.ContractPaymentMilestones.Add(x);
            }
            foreach (var x in await db.ContractNegotiationComments.AsNoTracking().Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == from && x.ContractNegotiationCommentState == 0).ToListAsync(ct))
            {
                db.ContractNegotiationComments.Add(new() { ContractDocumentId = documentId, ContractVersionId = next.ContractVersionId,
                    ContractNegotiationCommentTermid = x.ContractNegotiationCommentTermid is { } oldTerm && termMap.TryGetValue(oldTerm, out var mapped) ? checked((int)mapped.ContractTermTermid) : null,
                    ContractNegotiationCommentCarriedforwardfromcommentid = checked((int)x.ContractNegotiationCommentId), ContractNegotiationCommentCarriedforwardfromversionid = checked((int)from),
                    ContractNegotiationCommentSource = x.ContractNegotiationCommentSource, ContractNegotiationCommentContent = x.ContractNegotiationCommentContent,
                    AuthorCustomerId = x.AuthorCustomerId, RecordedByEmployeeId = x.RecordedByEmployeeId, CreatedAt = now });
            }
            foreach (var x in await db.ContractDynamicValues.AsNoTracking().Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == from).ToListAsync(ct))
            { x.ContractDynamicValueId = 0; x.ContractVersionId = next.ContractVersionId; db.ContractDynamicValues.Add(x); }
            doc.ContractDocumentCurrentVersionId = next.ContractVersionId; doc.UpdatedDate = now;
            ReviewData.Audit(db, "CREATE_VERSION", "CONTRACT_DOCUMENTS", documentId, "EMPLOYEE", actor.Id, now);
            await db.SaveChangesAsync(ct);
            return 0;
        }, ct);
        return (await drafts.Draft(documentId, ct))!;
    }

    public Task<ReviewSummary> Followup(long reviewId, FollowupRequest request, InternalUser actor, CancellationToken ct) => ReviewData.InTransaction(db, async () =>
    {
        var review = await db.ContractReviewRequests.SingleOrDefaultAsync(r => r.ContractReviewRequestId == reviewId, ct) ?? throw new InternalAppException(404, "Không tìm thấy lần gửi.");
        var doc = await OwnedDocument(review.ContractDocumentId, actor, ct);
        ReviewData.Expect(review.Rowversion, request.RowVersion);
        if (doc.ContractDocumentStatus != ContractDocumentStatus.Rejected) throw new InternalAppException(409, "Hồ sơ không ở trạng thái chờ quyết định sau khi khách từ chối.");
        int status;
        try { status = ContractReviewPolicy.Followup(review.ContractReviewRequestStatus, review.ContractReviewRequestFollowupDecision, request.Decision ?? ""); }
        catch (InvalidOperationException e) { throw new InternalAppException(409, e.Message); }
        var now = DateTime.UtcNow;
        var note = InternalDraftPolicy.Text(request.Note, 2000);
        review.ContractReviewRequestFollowupDecision = request.Decision; review.ContractReviewRequestFollowupNote = note.Length == 0 ? null : note;
        review.FollowupDecidedByEmployeeId = actor.Id; review.FollowupDecidedAt = now; review.UpdatedAt = now;
        doc.ContractDocumentStatus = status; doc.UpdatedDate = now;
        ReviewData.Audit(db, "REVIEW_FOLLOWUP_" + request.Decision, "CONTRACT_REVIEW_REQUEST", reviewId, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return (await Summaries(doc.ContractDocumentId, ct)).Single(r => r.Id == reviewId);
    }, ct);

    public async Task<ReviewComment> EmployeeComment(long reviewId, CommentInput input, InternalUser actor, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(r => r.ContractReviewRequestId == reviewId, ct) ?? throw new InternalAppException(404, "Không tìm thấy lần gửi.");
        await OwnedDocument(review.ContractDocumentId, actor, ct);
        var comment = await ReviewData.NewComment(db, review, input, ct);
        comment.RecordedByEmployeeId = actor.Id; comment.ContractNegotiationCommentSource = "InternalNote";
        await db.SaveChangesAsync(ct);
        return (await ReviewData.Comments(db, [reviewId], ct))[reviewId].Single(c => c.Id == comment.ContractNegotiationCommentId);
    }

    public async Task ResolveComment(long commentId, InternalUser actor, CancellationToken ct)
    {
        var comment = await db.ContractNegotiationComments.SingleOrDefaultAsync(c => c.ContractNegotiationCommentId == commentId, ct) ?? throw new InternalAppException(404, "Không tìm thấy góp ý.");
        await OwnedDocument(comment.ContractDocumentId, actor, ct);
        comment.ContractNegotiationCommentState = 1; comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetTemporaryPassword(long customerId, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageAll && !await db.ContractDocuments.AnyAsync(d => d.CustomerId == customerId && d.EmployeeId == actor.Id && d.TenantId == null, ct))
            throw new InternalAppException(403, "Bạn không phụ trách khách hàng này.");
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, ct) ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");
        if (customer.CustomerPersonalPhoneVerifiedAt is not null) throw new InternalAppException(409, "Khách đã đổi mật khẩu tạm; không cấp lại.");
        var phone = PhoneNumber.Normalize(customer.CustomerPersonalPhone) ?? throw new InternalAppException(409, "Khách hàng chưa có số điện thoại hợp lệ.");
        var now = DateTime.UtcNow;
        var since = now.AddHours(-1);
        if (await db.SmsOutboxes.CountAsync(s => s.CustomerId == customerId && s.SmsOutboxPurpose == SmsPurpose.TemporaryPassword && s.CreatedAt > since, ct) >= 3)
            throw new InternalAppException(429, "Đã cấp lại mật khẩu tạm 3 lần trong 1 giờ. Thử lại sau.");
        IssueTemporaryPassword(customer, phone, null, actor.Id, now);
        ReviewData.Audit(db, "RESET_TEMP_PASSWORD", "CUSTOMER", customerId, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
    }

    private void IssueTemporaryPassword(Customer customer, string phone, long? documentId, long actor, DateTime now)
    {
        var password = TemporaryPassword.Generate(); // never returned or logged
        customer.CustomerPasswordHash = new PasswordHasher<Customer>().HashPassword(customer, password);
        customer.UpdatedAt = now; customer.UpdatedBy = actor;
        sms.Enqueue(db, SmsPurpose.TemporaryPassword, customer.CustomerId, documentId, phone, sms.TemporaryPasswordMessage(PortalUrl, phone, password), now);
    }

    private async Task<ContractDocument> OwnedDocument(long id, InternalUser actor, CancellationToken ct)
    {
        var doc = await db.ContractDocuments.SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct) ?? throw new InternalAppException(404, "Không tìm thấy hồ sơ.");
        InternalDraftService.RequireAccess(actor, doc.EmployeeId);
        return doc;
    }

    private Task<ContractReviewRequest?> Latest(long documentId, CancellationToken ct) =>
        db.ContractReviewRequests.Where(r => r.ContractDocumentId == documentId).OrderByDescending(r => r.ContractReviewRequestId).FirstOrDefaultAsync(ct);

    private async Task<List<ReviewSummary>> Summaries(long documentId, CancellationToken ct)
    {
        var rows = await (from r in db.ContractReviewRequests.AsNoTracking()
            join v in db.ContractVersions on r.ContractVersionId equals v.ContractVersionId
            join e in db.Employees on r.SentByEmployeeId equals e.EmployeeId
            where r.ContractDocumentId == documentId orderby r.ContractReviewRequestId descending
            select new { r, v.ContractVersionVersionNo, e.EmployeeDisplayName }).ToListAsync(ct);
        var comments = await ReviewData.Comments(db, rows.Select(x => x.r.ContractReviewRequestId).ToList(), ct);
        var links = await db.ContractAccessLinks.AsNoTracking().Where(l => l.ContractDocumentId == documentId && l.RevokedAt == null).ToDictionaryAsync(l => l.ContractVersionId, l => l.ExpiresAt, ct);
        return rows.Select(x => new ReviewSummary(x.r.ContractReviewRequestId, x.r.ContractVersionId, x.ContractVersionVersionNo, x.r.ContractReviewRequestStatus,
            x.r.SentAt, x.EmployeeDisplayName, x.r.ContractReviewRequestMessage, x.r.FirstViewedAt, x.r.ContractReviewRequestCustomerNote, x.r.RespondedAt,
            x.r.ContractReviewRequestFollowupDecision, x.r.ContractReviewRequestFollowupNote, x.r.FollowupDecidedAt, Convert.ToBase64String(x.r.Rowversion),
            comments.GetValueOrDefault(x.r.ContractReviewRequestId) ?? [], links.TryGetValue(x.r.ContractVersionId, out var expires) ? expires : null)).ToList();
    }
}

public sealed class CustomerPortalRepository(MasterDbContext db, IOptions<EsmsOptions> options) : ICustomerPortalRepository
{
    private static readonly PasswordHasher<Customer> Hasher = new();
    private static readonly Customer Dummy = new();
    private static readonly string DummyHash = Hasher.HashPassword(Dummy, Guid.NewGuid().ToString("N"));

    public async Task<CustomerSession?> Login(string phone, string password, CancellationToken ct)
    {
        var normalized = PhoneNumber.Normalize(phone);
        var customer = normalized is null ? null : await db.Customers.FirstOrDefaultAsync(c => c.CustomerPersonalPhone == normalized && c.CustomerStatus == 1, ct);
        if (customer is null) { Hasher.VerifyHashedPassword(Dummy, DummyHash, password); return null; } // same cost as a real check
        if (Hasher.VerifyHashedPassword(customer, customer.CustomerPasswordHash, password) == PasswordVerificationResult.Failed) return null;
        var now = DateTime.UtcNow;
        if (customer.CustomerPersonalPhoneVerifiedAt is null)
        {
            var issued = await db.SmsOutboxes.Where(s => s.CustomerId == customer.CustomerId && s.SmsOutboxPurpose == SmsPurpose.TemporaryPassword)
                .OrderByDescending(s => s.CreatedAt).Select(s => (DateTime?)s.CreatedAt).FirstOrDefaultAsync(ct);
            if (issued is null || issued < now.AddHours(-options.Value.TemporaryPasswordHours))
                throw new InternalAppException(403, "Mật khẩu tạm đã hết hạn, liên hệ nhân viên phụ trách.", "TEMP_PASSWORD_EXPIRED");
        }
        customer.CustomerLastLoginAt = now;
        await db.SaveChangesAsync(ct);
        return new(customer.CustomerId, customer.CustomerDisplayName, customer.CustomerPersonalPhoneVerifiedAt is null);
    }

    public async Task<ReviewLinkAccess> OpenLink(string? token, CancellationToken ct)
    {
        var hash = ReviewLink.IsToken(token) ? ReviewLink.Hash(token!) : null;
        // shortcut: CONTRACT_ACCESS_LINK has no index on the token hash, so this scans; add one if the table grows large.
        var row = hash is null ? null : await (from l in db.ContractAccessLinks.AsNoTracking()
            join p in db.ContractPhoneVerifies on l.ContractAccessLinkVerificationPhoneId equals p.ContractPhoneVerifyId
            join r in db.ContractReviewRequests on new { l.ContractDocumentId, l.ContractVersionId } equals new { r.ContractDocumentId, r.ContractVersionId }
            join c in db.Customers on r.CustomerId equals c.CustomerId
            where l.ContractAccessLinkTokenhash == hash && l.RevokedAt == null && c.CustomerStatus == 1
            select new ReviewLinkAccess(l.ContractAccessLinkId, r.ContractReviewRequestId, r.CustomerId, p.ContractPhoneVerifyPhoneNumberNormalized, l.ExpiresAt)).FirstOrDefaultAsync(ct);
        if (row is null) throw new InternalAppException(404, "Link không hợp lệ hoặc đã được thay bằng link mới.", "LINK_INVALID");
        if (row.ExpiresAt <= DateTime.UtcNow) throw new InternalAppException(410, "Link đã hết hạn. Liên hệ nhân viên phụ trách để nhận link mới.", "LINK_EXPIRED");
        return row;
    }

    public Task<bool> LinkActive(long reviewId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return (from r in db.ContractReviewRequests
            join l in db.ContractAccessLinks on new { r.ContractDocumentId, r.ContractVersionId } equals new { l.ContractDocumentId, l.ContractVersionId }
            where r.ContractReviewRequestId == reviewId && l.RevokedAt == null && l.ExpiresAt > now select l).AnyAsync(ct);
    }

    public async Task RecordLinkVerified(ReviewLinkAccess link, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await db.ContractAccessLinks.SingleAsync(l => l.ContractAccessLinkId == link.LinkId, ct);
        row.ActivatedAt ??= now;
        ReviewData.Audit(db, "REVIEW_LINK_OTP_VERIFIED", "CONTRACT_REVIEW_REQUEST", link.ReviewId, "CUSTOMER", link.CustomerId, now);
        await db.SaveChangesAsync(ct);
    }

    public Task<CustomerSession?> Get(long customerId, CancellationToken ct) => db.Customers.AsNoTracking()
        .Where(c => c.CustomerId == customerId && c.CustomerStatus == 1)
        .Select(c => new CustomerSession(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhoneVerifiedAt == null)).FirstOrDefaultAsync(ct);

    public async Task ChangePassword(long customerId, CustomerChangePasswordInput input, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, ct) ?? throw new InternalAppException(401, "Vui lòng đăng nhập.");
        if (string.IsNullOrEmpty(input.CurrentPassword) || input.CurrentPassword.Length > 200
            || Hasher.VerifyHashedPassword(customer, customer.CustomerPasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new ArgumentException("Mật khẩu hiện tại không đúng.");
        if (!CustomerPasswordRules.IsValid(input.NewPassword)) throw new ArgumentException(CustomerPasswordRules.Message);
        if (input.NewPassword == input.CurrentPassword) throw new ArgumentException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        var now = DateTime.UtcNow;
        customer.CustomerPasswordHash = Hasher.HashPassword(customer, input.NewPassword!);
        customer.CustomerPersonalPhoneVerifiedAt ??= now; // receiving the SMS proved phone ownership
        customer.UpdatedAt = now;
        ReviewData.Audit(db, "CHANGE_PASSWORD", "CUSTOMER", customerId, "CUSTOMER", customerId, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<CustomerReviewSummary>> Reviews(long customerId, CancellationToken ct)
    {
        var rows = await (from r in db.ContractReviewRequests.AsNoTracking()
            join d in db.ContractDocuments on r.ContractDocumentId equals d.ContractDocumentId
            join v in db.ContractVersions on r.ContractVersionId equals v.ContractVersionId
            where r.CustomerId == customerId
            select new CustomerReviewSummary(r.ContractReviewRequestId, d.ContractDocumentName, v.ContractVersionVersionNo, r.ContractReviewRequestStatus, r.SentAt, r.RespondedAt))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => ContractReviewStatus.IsOpen(r.Status)).ThenByDescending(r => r.SentAt).ToList();
    }

    public async Task<CustomerReviewDetail> Review(long customerId, long reviewId, CancellationToken ct)
    {
        var review = await Own(customerId, reviewId, ct);
        if (review.ContractReviewRequestStatus == ContractReviewStatus.Sent)
        {
            review.ContractReviewRequestStatus = ContractReviewStatus.Viewed; review.FirstViewedAt = DateTime.UtcNow; review.UpdatedAt = review.FirstViewedAt;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); review = await Own(customerId, reviewId, ct); } // a parallel view or response won
        }
        return await Detail(review, ct);
    }

    public async Task<ReviewComment> Comment(long customerId, long reviewId, CommentInput input, CancellationToken ct)
    {
        var review = await Own(customerId, reviewId, ct);
        if (ContractReviewPolicy.CanRespond(review.ContractReviewRequestStatus) is { } error) throw new InternalAppException(409, error);
        var comment = await ReviewData.NewComment(db, review, input, ct);
        comment.AuthorCustomerId = customerId; comment.ContractNegotiationCommentSource = "ExternalFeedback";
        await db.SaveChangesAsync(ct);
        return (await ReviewData.Comments(db, [reviewId], ct))[reviewId].Single(c => c.Id == comment.ContractNegotiationCommentId);
    }

    public async Task<CustomerReviewDetail> Respond(long customerId, long reviewId, CustomerRespondInput input, string? ip, string? userAgent, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            var review = await Own(customerId, reviewId, ct);
            ReviewData.Expect(review.Rowversion, input.RowVersion);
            if (input.DocumentHash != review.ContractReviewRequestDocumentHash) throw new InternalAppException(409, "Nội dung hợp đồng đã thay đổi. Tải lại trang để xem bản mới nhất.");
            var note = InternalDraftPolicy.Text(input.Note, 4000);
            (string Review, int Document) next;
            try { next = ContractReviewPolicy.Respond(review.ContractReviewRequestStatus, input.Decision ?? "", note); }
            catch (InvalidOperationException e) { throw new InternalAppException(409, e.Message); }
            var doc = await db.ContractDocuments.SingleAsync(d => d.ContractDocumentId == review.ContractDocumentId, ct);
            var now = DateTime.UtcNow;
            review.ContractReviewRequestStatus = next.Review; review.ContractReviewRequestCustomerNote = note.Length == 0 ? null : note;
            review.RespondedByCustomerId = customerId; review.RespondedAt = now; review.UpdatedAt = now;
            review.RespondedIpAddress = ip is { Length: > 45 } ? ip[..45] : ip; review.RespondedUserAgent = userAgent is { Length: > 1000 } ? userAgent[..1000] : userAgent;
            doc.ContractDocumentStatus = next.Document; doc.UpdatedDate = now;
            var approved = next.Review == ContractReviewStatus.Approved;
            if (approved)
            {
                var workflow = await db.ApprovalWorkflows.Where(w => w.ApprovalWorkflowCode == ContractReviewRepository.InternalCheckWorkflow && w.ApprovalWorkflowIsactive)
                    .Select(w => (long?)w.ApprovalWorkflowId).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Missing CONTRACT_INTERNAL_CHECK workflow; seed it in the MASTER database script.");
                db.ContractApprovalRequests.Add(new ContractApprovalRequest { ContractDocumentId = doc.ContractDocumentId, ContractVersionId = review.ContractVersionId,
                    ApprovalWorkflowId = workflow, SubmittedByEmployeeId = doc.EmployeeId, SubmittedAt = now, ContractApprovalRequestStatus = 0, ContractReviewRequestId = reviewId });
            }
            var recipients = new HashSet<long> { doc.EmployeeId };
            if (approved) recipients.UnionWith(await (from er in db.EmployeeRoles join r in db.SystemRoles on er.SystemRoleId equals r.SystemRoleId
                join e in db.Employees on er.EmployeeId equals e.EmployeeId
                where r.SystemRoleCode == "INTERNAL_OFFICE" && r.SystemRoleStatus == "ACTIVE" && e.EmployeeStatus == "ACTIVE" select er.EmployeeId).ToListAsync(ct));
            var title = next.Review switch { ContractReviewStatus.Approved => "Khách đã đồng ý hợp đồng", ContractReviewStatus.ChangesRequested => "Khách yêu cầu sửa hợp đồng", _ => "Khách từ chối hợp đồng" };
            var notification = new Notification { NotificationEventkey = $"review:{reviewId}:{next.Review}", NotificationModulecode = "CONTRACT",
                NotificationEventcode = "REVIEW_" + next.Review, NotificationSubjecttype = "CONTRACT_DOCUMENT", NotificationSubjectid = doc.ContractDocumentId,
                NotificationTitle = title, NotificationBody = doc.ContractDocumentName.Length > 1000 ? doc.ContractDocumentName[..1000] : doc.ContractDocumentName, NotificationCreatedatutc = now };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(ct);
            foreach (var employee in recipients) db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notification.NotificationId, EmployeeId = employee, NotificationRecipientCreatedatutc = now });
            ReviewData.Audit(db, "REVIEW_" + next.Review, "CONTRACT_REVIEW_REQUEST", reviewId, "CUSTOMER", customerId, now);
            await db.SaveChangesAsync(ct);
            return 0;
        }, ct);
        db.ChangeTracker.Clear();
        return await Detail(await Own(customerId, reviewId, ct), ct);
    }

    // Always scoped by the session's customer id; another customer's review is reported as missing.
    private async Task<ContractReviewRequest> Own(long customerId, long reviewId, CancellationToken ct) =>
        await db.ContractReviewRequests.SingleOrDefaultAsync(r => r.ContractReviewRequestId == reviewId && r.CustomerId == customerId, ct)
        ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");

    private async Task<CustomerReviewDetail> Detail(ContractReviewRequest review, CancellationToken ct)
    {
        var doc = await db.ContractDocuments.AsNoTracking().SingleAsync(d => d.ContractDocumentId == review.ContractDocumentId, ct);
        var versionNo = await db.ContractVersions.Where(v => v.ContractVersionId == review.ContractVersionId).Select(v => v.ContractVersionVersionNo).SingleAsync(ct);
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleAsync(x => x.ContractDocumentId == review.ContractDocumentId
            && x.ContractVersionId == review.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
        content.CustomerId = 0; content.TemplateId = 0; // internal ids stay internal
        var comments = await ReviewData.Comments(db, [review.ContractReviewRequestId], ct);
        return new(review.ContractReviewRequestId, doc.ContractDocumentName, versionNo, review.ContractReviewRequestStatus, review.SentAt, review.ContractReviewRequestMessage,
            review.ContractReviewRequestCustomerNote, review.RespondedAt, review.ContractReviewRequestDocumentHash, Convert.ToBase64String(review.Rowversion), content,
            comments.GetValueOrDefault(review.ContractReviewRequestId) ?? []);
    }
}

internal static class ReviewData
{
    public static async Task<T> InTransaction<T>(MasterDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var result = await work();
                await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateConcurrencyException) { throw new InternalAppException(409, "Dữ liệu vừa được cập nhật ở nơi khác. Tải lại rồi thử lại."); }
    }

    /// <summary>Client rowversion must match; EF then also checks the loaded value on save.</summary>
    public static void Expect(byte[] current, string? clientRowVersion)
    {
        if (clientRowVersion is null || clientRowVersion != Convert.ToBase64String(current))
            throw new InternalAppException(409, "Dữ liệu vừa được cập nhật ở nơi khác. Tải lại rồi thử lại.");
    }

    public static void Audit(MasterDbContext db, string action, string entity, long id, string actorType, long actor, DateTime now) =>
        db.AuditLogs.Add(new AuditLog { ActionType = action, EntityName = entity, EntityId = id.ToString(), ActorType = actorType, ActorId = actor, CreatedAt = now });

    public static async Task<ContractNegotiationComment> NewComment(MasterDbContext db, ContractReviewRequest review, CommentInput input, CancellationToken ct)
    {
        var content = InternalDraftPolicy.Text(input.Content, 4000);
        if (content.Length == 0) throw new ArgumentException("Nhập nội dung góp ý.");
        int? termId = null, parentId = null;
        if (!string.IsNullOrWhiteSpace(input.TermKey))
            termId = checked((int)(await db.ContractTerms.Where(t => t.ContractDocumentId == review.ContractDocumentId && t.ContractVersionId == review.ContractVersionId
                && t.ContractTermTermcode == input.TermKey && t.ContractTermIsnegotiable).Select(t => (long?)t.ContractTermTermid).FirstOrDefaultAsync(ct) ?? throw new ArgumentException("Điều khoản không thuộc phiên bản này hoặc không cho phép thương lượng.")));
        if (input.ParentCommentId is { } parent)
        {
            if (!await db.ContractNegotiationComments.AnyAsync(c => c.ContractNegotiationCommentId == parent && c.ContractReviewRequestId == review.ContractReviewRequestId, ct))
                throw new ArgumentException("Góp ý được trả lời không thuộc lần gửi này.");
            parentId = checked((int)parent);
        }
        var comment = new ContractNegotiationComment { ContractDocumentId = review.ContractDocumentId, ContractVersionId = review.ContractVersionId,
            ContractNegotiationCommentTermid = termId, ContractNegotiationCommentParentcommentid = parentId, ContractNegotiationCommentContent = content,
            ContractReviewRequestId = review.ContractReviewRequestId, ContractNegotiationCommentState = 0, CreatedAt = DateTime.UtcNow, ContractNegotiationCommentSource = "" };
        db.ContractNegotiationComments.Add(comment);
        return comment;
    }

    public static async Task<Dictionary<long, List<ReviewComment>>> Comments(MasterDbContext db, List<long> reviewIds, CancellationToken ct)
    {
        var rows = await (from c in db.ContractNegotiationComments.AsNoTracking()
            where c.ContractReviewRequestId != null && reviewIds.Contains(c.ContractReviewRequestId.Value)
            join t in db.ContractTerms on (long?)c.ContractNegotiationCommentTermid equals t.ContractTermTermid into terms from t in terms.DefaultIfEmpty()
            join e in db.Employees on c.RecordedByEmployeeId equals e.EmployeeId into employees from e in employees.DefaultIfEmpty()
            orderby c.ContractNegotiationCommentId
            select new { c, TermKey = t == null ? null : t.ContractTermTermcode, TermTitle = t == null ? null : t.ContractTermTermtitle, Employee = e == null ? null : e.EmployeeDisplayName })
            .ToListAsync(ct);
        return rows.GroupBy(x => x.c.ContractReviewRequestId!.Value).ToDictionary(g => g.Key, g => g.Select(x => new ReviewComment(x.c.ContractNegotiationCommentId,
            x.TermKey, x.TermTitle, x.c.ContractNegotiationCommentParentcommentid, x.c.ContractNegotiationCommentContent,
            x.c.AuthorCustomerId is null ? x.Employee ?? "Nhân viên" : "Khách hàng", x.c.AuthorCustomerId is not null, x.c.ContractNegotiationCommentState == 1, x.c.CreatedAt)).ToList());
    }
}
