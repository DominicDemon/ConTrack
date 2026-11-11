using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SYSTEM_PERMISSION
{
    public long system_permission_id { get; set; }

    public long system_permission_group_id { get; set; }

    public string system_permission_code { get; set; } = null!;

    public string system_permission_name { get; set; } = null!;

    public string? system_permission_resource { get; set; }

    public string? system_permission_action { get; set; }

    public string? system_permission_description { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
