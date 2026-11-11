using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_SIGNED_EVIDENCE
{
    public long contract_signed_evidence_id { get; set; }

    public long contract_document_file_id { get; set; }

    public int contract_signed_evidence_status { get; set; }

    public DateOnly contract_signed_evidence_signdate { get; set; }

    public long uploaded_by_employeeid { get; set; }

    public DateTime uploaded_at { get; set; }

    public long? superseded_by_employee_id { get; set; }

    public DateTime? superseded_at { get; set; }

    public long? supersedes_evidence_id { get; set; }

    public string? contract_signed_evidence_supersedereason { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
