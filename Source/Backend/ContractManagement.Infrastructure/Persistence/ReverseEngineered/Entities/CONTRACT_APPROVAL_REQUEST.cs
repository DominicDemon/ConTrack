using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_APPROVAL_REQUEST
{
    public long contract_approval_request_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public long approval_workflow_id { get; set; }

    public long submitted_by_employee_id { get; set; }

    public DateTime submitted_at { get; set; }

    public int contract_approval_request_status { get; set; }

    public DateTime? contract_approval_request_resolved_at { get; set; }

    public long? resolved_by_employee_id { get; set; }

    public string? contract_approval_request_decisioncomment { get; set; }

    public byte[] contract_approval_request_rowversion { get; set; } = null!;
}
