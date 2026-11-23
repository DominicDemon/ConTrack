using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record SmsSendResult(string CodeResult, string? SmsId, string? ErrorMessage);
public interface ISmsSender
{
    /// <summary>CodeResult "100" means eSMS accepted the message. Throws only for transient transport failures.</summary>
    Task<SmsSendResult> SendAsync(string phone, string content, string requestId, CancellationToken ct);
}

// Employee side
public sealed record SendReviewRequest(string? Message, string? RowVersion, string? VersionRowVersion, bool ContentConfirmed = false, DateTimeOffset? LinkExpiresAt = null);
public sealed record ResendLinkRequest(DateTimeOffset? LinkExpiresAt, string? RowVersion);
public sealed record NewVersionRequest(string? RowVersion, string? ChangeNote);
public static class ContractVersionNotes
{
    public const string WorkingPrefix = "[WORKING]";
    public static string ForWorking(string? note) => WorkingPrefix + (string.IsNullOrWhiteSpace(note) ? "" : note.Trim());
    public static bool IsWorking(string? note) => note?.StartsWith(WorkingPrefix, StringComparison.Ordinal) == true;
    public static string? Display(string? note) => IsWorking(note) ? note![WorkingPrefix.Length..] : note;
}
public sealed record FollowupRequest(string? Decision, string? Note, string? RowVersion);
public sealed record InternalApprovalDecisionRequest(bool Approve, string? Comment);
public sealed record InternalApprovalState(long Id, int Status, DateTime SubmittedAt, string WorkflowName, string? Comment, bool SigningStarted);
/// <summary>Result of creating a customer account or reissuing its temporary password; the password itself only appears in Development when the email failed.</summary>
public sealed record ResetTemporaryPasswordResult(bool EmailSent, string? EmailMasked, string? DevelopmentPhone, string? DevelopmentPassword);
public sealed record SigningStartResult(long CompanySignatureId, long DocumentId, long VersionId, string FileName, DateTime StartedAt,
    string? CustomerSigningUrl = null, DateTime? CustomerLinkExpiresAt = null);
public sealed record CompanySigningQueueItem(long SignatureId, long DocumentId, long VersionId, string Code, string ContractName, string CustomerName, string SignerName, DateTime CreatedAt);
public sealed record CompanySignatureRequest(string SignaturePngBase64, bool Confirmed);
public sealed record StartCompanySigningRequest(string SignerName, string SignerPhone, string? SignerTitle);
/// <param name="Section">A key of ContractReviewPolicy.CommentSections when the comment is about a part of the contract other than a term.</param>
public sealed record CommentInput(string? TermKey, long? ParentCommentId, string? Content, string? Section = null);
/// <param name="TermTitle">The term's title, or the section's name for a comment by section.</param>
/// <param name="Carried">Copied from the previous version (an unresolved earlier comment), not written on this one.</param>
public sealed record ReviewComment(long Id, string? TermKey, string? TermTitle, long? ParentId, string Content, string AuthorName, bool FromCustomer, bool Resolved, DateTime CreatedAt, string? Section = null, bool Carried = false);
public sealed record ReviewSummary(long Id, long VersionId, long VersionNo, string Status, DateTime SentAt, string SentByName, string? Message, DateTime? FirstViewedAt,
    string? CustomerNote, DateTime? RespondedAt, string? FollowupDecision, string? FollowupNote, DateTime? FollowupDecidedAt, string RowVersion, List<ReviewComment> Comments,
    DateTime? LinkExpiresAt, bool? LinkEmailSent = null); // LinkEmailSent is only set on the send/resend response
public sealed record DraftVersionHistoryItem(long Id, long VersionNo, string? ChangeNote, DateTime CreatedAt, bool IsCurrent, bool Locked, string? SnapshotHash);

public interface IContractReviewRepository
{
    Task<ReviewSummary> SendReview(long documentId, SendReviewRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewSummary> ResendLink(long reviewId, ResendLinkRequest request, InternalUser actor, CancellationToken ct);
    Task<List<ReviewSummary>> Reviews(long documentId, InternalUser actor, CancellationToken ct);
    Task<List<DraftVersionHistoryItem>> VersionHistory(long documentId, InternalUser actor, CancellationToken ct);
    Task<DraftRecord> CreateVersion(long documentId, NewVersionRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewSummary> Followup(long reviewId, FollowupRequest request, InternalUser actor, CancellationToken ct);
    Task<InternalApprovalState?> InternalApproval(long documentId, InternalUser actor, CancellationToken ct);
    Task<InternalApprovalState> ResolveInternalApproval(long documentId, InternalApprovalDecisionRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewComment> EmployeeComment(long reviewId, CommentInput input, InternalUser actor, CancellationToken ct);
    Task ResolveComment(long commentId, InternalUser actor, CancellationToken ct);
    Task<ResetTemporaryPasswordResult> ResetTemporaryPassword(long customerId, InternalUser actor, CancellationToken ct);
    Task<ResetTemporaryPasswordResult> CreateCustomerAccount(long customerId, InternalUser actor, CancellationToken ct);
}

public interface IInternalSigningService
{
    Task<IReadOnlyList<CompanySigningQueueItem>> Queue(InternalUser actor, CancellationToken ct);
    Task<SigningStartResult> Start(long documentId, StartCompanySigningRequest request, InternalUser actor, CancellationToken ct);
    Task<SigningStartResult?> StartAfterFinalApproval(long documentId, InternalUser actor, CancellationToken ct);
    Task<byte[]> CompanyDocument(long signatureId, InternalUser actor, CancellationToken ct);
    Task<SigningStartResult> SignCompany(long signatureId, CompanySignatureRequest request, InternalUser actor, CancellationToken ct);
    /// <summary>The approved PDF of a business contract, printed to be signed and stamped with the red seal on paper.</summary>
    Task<(byte[] Pdf, string FileName)> PrintPdf(long documentId, InternalUser actor, CancellationToken ct);
}

// Customer portal
public sealed record CustomerSession(long Id, string Name, bool MustChangePassword);
/// <param name="FirstViewedAt">Null = the customer has not opened this version yet (an unread item of the portal bell).</param>
/// <summary>One portal bell item. Key is stable per event (the browser remembers which keys were read); ReviewId opens the contract.</summary>
public sealed record CustomerContractNotice(string Key, string Code, string Title, string Body, long ReviewId, DateTime At);
public sealed record CustomerReviewSummary(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, DateTime? RespondedAt,
    DateTime? FirstViewedAt = null, bool IsSigned = false, string? ParentName = null, int DocumentType = 1, string? DocumentTypeName = null);
public sealed record CustomerReviewDetail(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, string? Message, string? CustomerNote,
    DateTime? RespondedAt, string DocumentHash, string RowVersion, DraftContent Content, List<ReviewComment> Comments, bool IsSigned = false,
    List<CustomerRelatedDocument>? Related = null);
/// <summary>A document linked to the one the customer opened: its contract (Parent) or an attached appendix/handover record (Attachment).
/// ReviewId is null while the company has not sent it to the customer yet.</summary>
public sealed record CustomerRelatedDocument(string Relation, string Name, int DocumentType, long? ReviewId, string? Status, bool IsSigned, string? DocumentTypeName = null);
public sealed record CustomerLoginInput(string? Phone, string? Password);
public sealed record CustomerChangePasswordInput(string? CurrentPassword, string? NewPassword);
public sealed record CustomerRespondInput(string? Decision, string? Note, string? DocumentHash, string? RowVersion);
public sealed record ReviewLinkInput(string? Token, string? Phone = null, string? Otp = null);
public sealed record ReviewLinkAccess(long LinkId, long ReviewId, long CustomerId, string Phone, DateTime ExpiresAt);

public interface ICustomerPortalRepository
{
    /// <returns>Null for wrong phone/password. Throws 403 when the temporary password expired.</returns>
    Task<CustomerSession?> Login(string phone, string password, CancellationToken ct);
    Task<CustomerSession?> Get(long customerId, CancellationToken ct);
    /// <summary>Throws 404 for an unknown link and 410 for an expired one.</summary>
    Task<ReviewLinkAccess> OpenLink(string? token, CancellationToken ct);
    /// <summary>The open review's current link, for the employee in charge to resend the customer's code (audited as RESEND_REVIEW_OTP).</summary>
    Task<ReviewLinkAccess> ActiveLink(long reviewId, InternalUser actor, CancellationToken ct);
    Task<bool> LinkActive(long reviewId, CancellationToken ct);
    Task RecordLinkVerified(ReviewLinkAccess link, CancellationToken ct);
    Task ChangePassword(long customerId, CustomerChangePasswordInput input, CancellationToken ct);
    Task<List<CustomerReviewSummary>> Reviews(long customerId, CancellationToken ct);
    /// <summary>What the portal bell shows, derived from the customer's contracts (no table of its own; read state stays in the browser).</summary>
    Task<List<CustomerContractNotice>> Notices(long customerId, CancellationToken ct);
    Task<CustomerReviewDetail> Review(long customerId, long reviewId, CancellationToken ct);
    Task<ReviewComment> Comment(long customerId, long reviewId, CommentInput input, CancellationToken ct);
    Task<ReviewComment> EditComment(long customerId, long reviewId, long commentId, CommentInput input, CancellationToken ct);
    Task DeleteComment(long customerId, long reviewId, long commentId, CancellationToken ct);
    Task<CustomerReviewDetail> Respond(long customerId, long reviewId, CustomerRespondInput input, string? ip, string? userAgent, CancellationToken ct);
}
