using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ApprovalWorkflow
{
    public long ApprovalWorkflowId { get; set; }

    public string ApprovalWorkflowCode { get; set; } = null!;

    public string ApprovalWorkflowName { get; set; } = null!;

    public string? ApprovalWorkflowDescription { get; set; }

    public string ApprovalWorkflowObjecttype { get; set; } = null!;

    public bool ApprovalWorkflowIsactive { get; set; }

    public DateTime ApprovalWorkflowCreatedat { get; set; }

    public long? ApprovalWorkflowCreatedbyemployeeid { get; set; }

    public DateTime? ApprovalWorkflowUpdatedat { get; set; }

    public long? ApprovalWorkflowUpdatedbyemployeeid { get; set; }
}
