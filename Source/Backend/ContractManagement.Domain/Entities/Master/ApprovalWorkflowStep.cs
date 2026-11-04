using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ApprovalWorkflowStep
{
    public long ApprovalWorkflowStepId { get; set; }

    public long ApprovalWorkflowId { get; set; }

    public long ApprovalWorkflowStepOrder { get; set; }

    public string ApprovalWorkflowStepName { get; set; } = null!;

    public long SystemRoleId { get; set; }

    public bool ApprovalWorkflowStepIsrequired { get; set; }
}
