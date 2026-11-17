using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PLAN_VERSION
{
    public long plan_version_id { get; set; }

    public long plan_id { get; set; }

    public int plan_version_number { get; set; }

    public int plan_version_duration_days { get; set; }

    public decimal plan_version_price { get; set; }

    public string plan_version_currency { get; set; } = null!;

    public DateTime plan_version_effective_from { get; set; }

    public DateTime? plan_version_effective_to { get; set; }

    public string plan_version_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
