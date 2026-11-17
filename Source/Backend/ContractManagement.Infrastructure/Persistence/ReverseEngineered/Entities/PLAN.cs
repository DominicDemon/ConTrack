using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PLAN
{
    public long plan_id { get; set; }

    public long saas_type_id { get; set; }

    public long package_level_id { get; set; }

    public string plan_code { get; set; } = null!;

    public string plan_name { get; set; } = null!;

    public long? current_active_version_id { get; set; }

    public string plan_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
