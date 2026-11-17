using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_HANDOVER_RECORD
{
    public long contract_handover_record_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public long contract_template_id { get; set; }

    public string contract_handover_record_handovercode { get; set; } = null!;

    public DateOnly contract_handover_record_handover_at { get; set; }

    public DateOnly contract_handover_record_contractsigndate { get; set; }

    public string contract_handover_record_location { get; set; } = null!;

    public string? contract_handover_record_locationen { get; set; }

    public int contract_handover_record_status { get; set; }

    public string? contract_handover_record_snapshothash { get; set; }

    public long created_employee_id { get; set; }

    public DateTime created_at { get; set; }

    public long? updated_employee_id { get; set; }

    public DateTime? updated_at { get; set; }

    public long? finalized_by_employee_id { get; set; }

    public DateTime? finalized_at { get; set; }

    public long? signed_by_employee_id { get; set; }

    public DateTime? signed_at { get; set; }

    public long? cancelled_by_employee_id { get; set; }

    public DateTime? cancelled_at { get; set; }

    public string? contract_handover_record_cancellationreason { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
