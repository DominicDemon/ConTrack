using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class PromotionCustomerUsage
{
    public long CustomerUsageId { get; set; }

    public long PromotionId { get; set; }

    public long CustomerId { get; set; }

    public int TimesUsed { get; set; }

    public DateTime LastUsedAt { get; set; }
}
