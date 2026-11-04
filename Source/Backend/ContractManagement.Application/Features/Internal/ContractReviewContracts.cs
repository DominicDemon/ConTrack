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
public sealed record FollowupRequest(string? Decision, string? Note, string? RowVersion);
public sealed record CommentInput(string? TermKey, long? ParentCommentId, string? Content);
public sealed record ReviewComment(long Id, string? TermKey, string? TermTitle, long? ParentId, string Content, string AuthorName, bool FromCustomer, bool Resolved, DateTime CreatedAt);
public sealed record ReviewSummary(long Id, long VersionId, long VersionNo, string Status, DateTime SentAt, string SentByName, string? Message, DateTime? FirstViewedAt,
    string? CustomerNote, DateTime? RespondedAt, string? FollowupDecision, string? FollowupNote, DateTime? FollowupDecidedAt, string RowVersion, List<ReviewComment> Comments,
    DateTime? LinkExpiresAt, bool? LinkEmailSent = null); // LinkEmailSent is only set on the send/resend response

public interface IContractReviewRepository
{
    Task<ReviewSummary> SendReview(long documentId, SendReviewRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewSummary> ResendLink(long reviewId, ResendLinkRequest request, InternalUser actor, CancellationToken ct);
    Task<List<ReviewSummary>> Reviews(long documentId, InternalUser actor, CancellationToken ct);
    Task<DraftRecord> CreateVersion(long documentId, NewVersionRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewSummary> Followup(long reviewId, FollowupRequest request, InternalUser actor, CancellationToken ct);
    Task<ReviewComment> EmployeeComment(long reviewId, CommentInput input, InternalUser actor, CancellationToken ct);
    Task ResolveComment(long commentId, InternalUser actor, CancellationToken ct);
    Task ResetTemporaryPassword(long customerId, InternalUser actor, CancellationToken ct);
}

// Customer portal
public sealed record CustomerSession(long Id, string Name, bool MustChangePassword);
public sealed record CustomerReviewSummary(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, DateTime? RespondedAt);
public sealed record CustomerReviewDetail(long Id, string ContractName, long VersionNo, string Status, DateTime SentAt, string? Message, string? CustomerNote,
    DateTime? RespondedAt, string DocumentHash, string RowVersion, DraftContent Content, List<ReviewComment> Comments);
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
    Task<bool> LinkActive(long reviewId, CancellationToken ct);
    Task RecordLinkVerified(ReviewLinkAccess link, CancellationToken ct);
    Task ChangePassword(long customerId, CustomerChangePasswordInput input, CancellationToken ct);
    Task<List<CustomerReviewSummary>> Reviews(long customerId, CancellationToken ct);
    Task<CustomerReviewDetail> Review(long customerId, long reviewId, CancellationToken ct);
    Task<ReviewComment> Comment(long customerId, long reviewId, CommentInput input, CancellationToken ct);
    Task<CustomerReviewDetail> Respond(long customerId, long reviewId, CustomerRespondInput input, string? ip, string? userAgent, CancellationToken ct);
}
