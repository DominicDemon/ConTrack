using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class INVOICE_PROMOTION_DETAIL
{
    public long invoice_promotion_detail_id { get; set; }

    public long invoice_id { get; set; }

    public long promotion_id { get; set; }

    public decimal discount_value_applied { get; set; }

    public decimal actual_money_deducted { get; set; }

    public DateTime created_at { get; set; }
}
