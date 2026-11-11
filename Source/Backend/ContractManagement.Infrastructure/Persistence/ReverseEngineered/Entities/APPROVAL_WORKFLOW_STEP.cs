using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class APPROVAL_WORKFLOW_STEP
{
    public long approval_workflow_step_id { get; set; }

    public long approval_workflow_id { get; set; }

    public long approval_workflow_step_order { get; set; }

    public string approval_workflow_step_name { get; set; } = null!;

    public long system_role_id { get; set; }

    public bool approval_workflow_step_isrequired { get; set; }
}
