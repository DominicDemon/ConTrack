using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_ACCESS_LINK
{
    public long contract_access_link_id { get; set; }

    public long? tenant_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public long contract_access_link_verification_phone_id { get; set; }

    public string contract_access_link_tokenhash { get; set; } = null!;

    public string? contract_access_link_revocation_reason { get; set; }

    public long created_by_employee_id { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? activated_at { get; set; }

    public DateTime expires_at { get; set; }

    public DateTime? revoked_at { get; set; }

    public long? revoked_by_employee_id { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
