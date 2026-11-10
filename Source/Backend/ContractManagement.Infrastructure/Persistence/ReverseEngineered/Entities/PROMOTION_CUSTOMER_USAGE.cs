using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PROMOTION_CUSTOMER_USAGE
{
    public long customer_usage_id { get; set; }

    public long promotion_id { get; set; }

    public long customer_id { get; set; }

    public int times_used { get; set; }

    public DateTime last_used_at { get; set; }
}
