using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SYSTEM_PERMISSION_GROUP
{
    public long system_permission_group_id { get; set; }

    public string system_permission_group_code { get; set; } = null!;

    public string system_permission_group_name { get; set; } = null!;

    public string? system_permission_group_description { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
