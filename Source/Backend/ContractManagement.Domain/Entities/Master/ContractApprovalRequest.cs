using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractApprovalRequest
{
    [System.ComponentModel.DataAnnotations.Schema.Column("internal_stage")]
    public string? InternalStage { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("internal_content_hash")]
    public string? InternalContentHash { get; set; }

    public long ContractApprovalRequestId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long ApprovalWorkflowId { get; set; }

    public long SubmittedByEmployeeId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public int ContractApprovalRequestStatus { get; set; }

    public DateTime? ContractApprovalRequestResolvedAt { get; set; }

    public long? ResolvedByEmployeeId { get; set; }

    public string? ContractApprovalRequestDecisioncomment { get; set; }

    public long? ContractReviewRequestId { get; set; }

    public byte[] ContractApprovalRequestRowversion { get; set; } = null!;
}
