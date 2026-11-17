using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SAAS_PERMISSION
{
    public long permission_id { get; set; }

    public long permission_group_id { get; set; }

    public string permission_code { get; set; } = null!;

    public string permission_name { get; set; } = null!;

    public string? permission_description { get; set; }

    public int permission_point { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
