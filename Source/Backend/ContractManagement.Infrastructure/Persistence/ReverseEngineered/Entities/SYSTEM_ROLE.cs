using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SYSTEM_ROLE
{
    public long system_role_id { get; set; }

    public string system_role_code { get; set; } = null!;

    public string system_role_name { get; set; } = null!;

    public string system_role_scope { get; set; } = null!;

    public string? system_role_description { get; set; }

    public string system_role_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
