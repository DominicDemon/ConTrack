using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PROMOTION
{
    public long promotion_id { get; set; }

    public string promotion_code { get; set; } = null!;

    public string promotion_name { get; set; } = null!;

    public string? promotion_description { get; set; }

    public string promotion_discount_type { get; set; } = null!;

    public decimal promotion_discount_value { get; set; }

    public decimal? promotion_max_discount_amount { get; set; }

    public bool is_stackable { get; set; }

    public int stacking_priority { get; set; }

    public int max_usage_per_customer { get; set; }

    public int? promotion_usage_limit { get; set; }

    public int promotion_usage_count { get; set; }

    public DateTime promotion_start_at { get; set; }

    public DateTime promotion_end_at { get; set; }

    public string promotion_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
