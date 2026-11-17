namespace ContractManagement.Application.Features.Internal;

public sealed record ApprovalAction(string RowVersion, string VersionRowVersion, string? ApprovalRowVersion = null, string? Note = null);
public sealed record InternalCommentInput(string RowVersion, string VersionRowVersion, string Content, string? TermKey = null, long? ResolveId = null);
public sealed record ApprovalEvent(long Id, string Action, string Actor, DateTime At, string? Data);
public sealed record InternalApprovalView(string Stage, string? RowVersion, bool CanSubmit, bool CanDecide, List<ApprovalEvent> History);
