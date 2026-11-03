using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class InvoicePromotionDetail
{
    public long InvoicePromotionDetailId { get; set; }

    public long InvoiceId { get; set; }

    public long PromotionId { get; set; }

    public decimal DiscountValueApplied { get; set; }

    public decimal ActualMoneyDeducted { get; set; }

    public DateTime CreatedAt { get; set; }
}
