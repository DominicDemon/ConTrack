using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record SmsSendResult(string CodeResult, string? SmsId, string? ErrorMessage);
public interface ISmsSender
{
    /// <summary>CodeResult "100" means eSMS accepted the message. Throws only for transient transport failures.</summary>
    Task<SmsSendResult> SendAsync(string phone, string content, string requestId, CancellationToken ct);
}

// Employee side
public sealed record SendReviewRequest(string? Message, string? RowVersion, string? VersionRowVersion);
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
public sealed record ResetTemporaryPasswordResult(bool SmsSimulated, string? DevelopmentPhone, string? DevelopmentPassword);
public sealed record SigningStartResult(long CompanySignatureId, long DocumentId, long VersionId, string FileName, DateTime StartedAt,
    string? CustomerSigningUrl = null, DateTime? CustomerLinkExpiresAt = null);
public sealed record CompanySigningQueueItem(long SignatureId, long DocumentId, long VersionId, string Code, string ContractName, string CustomerName, string SignerName, DateTime CreatedAt);
public sealed record CompanySignatureRequest(string SignaturePngBase64, bool Confirmed);
public sealed record StartCompanySigningRequest(string SignerName, string SignerPhone, string? SignerTitle);
public sealed record CommentInput(string? TermKey, long? ParentCommentId, string? Content);
public sealed record ReviewComment(long Id, string? TermKey, string? TermTitle, long? ParentId, string Content, string AuthorName, bool FromCustomer, bool Resolved, DateTime CreatedAt);
public sealed record ReviewSummary(long Id, long VersionId, long VersionNo, string Status, DateTime SentAt, string SentByName, string? Message, DateTime? FirstViewedAt,
    string? CustomerNote, DateTime? RespondedAt, string? FollowupDecision, string? FollowupNote, DateTime? FollowupDecidedAt, string RowVersion, List<ReviewComment> Comments);
public sealed record DraftVersionHistoryItem(long Id, long VersionNo, string? ChangeNote, DateTime CreatedAt, bool IsCurrent, bool Locked, string? SnapshotHash);

public interface IContractReviewRepository
{
    Task<ReviewSummary> SendReview(long documentId, SendReviewRequest request, InternalUser actor, CancellationToken ct);
    Task<List<ReviewSummary>> Reviews(long documentId, InternalUser actor, CancellationToken ct);
    Task<List<DraftVersionHistoryItem>> VersionHistory(long documentId, InternalUser actor, CancellationToken ct);
    Task<DraftRecord> CreateVersion(long documentId, NewVersionRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewSummary> Followup(long reviewId, FollowupRequest request, InternalUser actor, CancellationToken ct);
    Task<InternalApprovalState?> InternalApproval(long documentId, InternalUser actor, CancellationToken ct);
    Task<InternalApprovalState> ResolveInternalApproval(long documentId, InternalApprovalDecisionRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewComment> EmployeeComment(long reviewId, CommentInput input, InternalUser actor, CancellationToken ct);
    Task ResolveComment(long commentId, InternalUser actor, CancellationToken ct);
    Task<ResetTemporaryPasswordResult> ResetTemporaryPassword(long customerId, InternalUser actor, CancellationToken ct);
}

public interface IInternalSigningService
{
    Task<IReadOnlyList<CompanySigningQueueItem>> Queue(InternalUser actor, CancellationToken ct);
    Task<SigningStartResult> Start(long documentId, StartCompanySigningRequest request, InternalUser actor, CancellationToken ct);
    Task<byte[]> CompanyDocument(long signatureId, InternalUser actor, CancellationToken ct);
    Task<SigningStartResult> SignCompany(long signatureId, CompanySignatureRequest request, InternalUser actor, CancellationToken ct);
}

// Customer portal
public sealed record CustomerSession(long Id, string Name, bool MustChangePassword);
public sealed record CustomerReviewSummary(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, DateTime? RespondedAt);
public sealed record CustomerReviewDetail(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, string? Message, string? CustomerNote,
    DateTime? RespondedAt, string DocumentHash, string RowVersion, DraftContent Content, List<ReviewComment> Comments);
public sealed record CustomerLoginInput(string? Phone, string? Password);
public sealed record CustomerChangePasswordInput(string? CurrentPassword, string? NewPassword);
public sealed record CustomerRespondInput(string? Decision, string? Note, string? DocumentHash, string? RowVersion);

public interface ICustomerPortalRepository
{
    /// <returns>Null for wrong phone/password. Throws 403 when the temporary password expired.</returns>
    Task<CustomerSession?> Login(string phone, string password, CancellationToken ct);
    Task<CustomerSession?> Get(long customerId, CancellationToken ct);
    Task ChangePassword(long customerId, CustomerChangePasswordInput input, CancellationToken ct);
    Task<List<CustomerReviewSummary>> Reviews(long customerId, CancellationToken ct);
    Task<CustomerReviewDetail> Review(long customerId, long reviewId, CancellationToken ct);
    Task<ReviewComment> Comment(long customerId, long reviewId, CommentInput input, CancellationToken ct);
    Task<CustomerReviewDetail> Respond(long customerId, long reviewId, CustomerRespondInput input, string? ip, string? userAgent, CancellationToken ct);
}
