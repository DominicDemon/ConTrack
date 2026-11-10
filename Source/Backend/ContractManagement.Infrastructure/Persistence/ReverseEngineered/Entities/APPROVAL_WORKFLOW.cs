using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class APPROVAL_WORKFLOW
{
    public long approval_workflow_id { get; set; }

    public string approval_workflow_code { get; set; } = null!;

    public string approval_workflow_name { get; set; } = null!;

    public string? approval_workflow_description { get; set; }

    public string approval_workflow_objecttype { get; set; } = null!;

    public bool approval_workflow_isactive { get; set; }

    public DateTime approval_workflow_createdat { get; set; }

    public long? approval_workflow_createdbyemployeeid { get; set; }

    public DateTime? approval_workflow_updatedat { get; set; }

    public long? approval_workflow_updatedbyemployeeid { get; set; }
}
