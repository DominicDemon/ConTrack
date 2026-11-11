using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_PHONE_VERIFY
{
    public long contract_phone_verify_id { get; set; }

    public long contract_document_id { get; set; }

    public string contract_phone_verify_phone_source { get; set; } = null!;

    public string contract_phone_verify_phone_number_normalized { get; set; } = null!;

    public string contract_phone_verify_reason { get; set; } = null!;

    public long created_by_employeeid { get; set; }

    public DateTime created_at { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
