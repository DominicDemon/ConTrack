using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_RENEWAL
{
    public long contract_renewal_id { get; set; }

    public long contract_id { get; set; }

    public DateTime? renewal_request_date { get; set; }

    public DateTime? old_expiration_date { get; set; }

    public DateTime? new_expiration_date { get; set; }

    public long? approved_by { get; set; }

    public string? contract_renewal { get; set; }

    public DateTime approved_at_date { get; set; }

    public byte[] contract_version_rowversion { get; set; } = null!;
}
