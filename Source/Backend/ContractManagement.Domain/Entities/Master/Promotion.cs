using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Promotion
{
    public long PromotionId { get; set; }

    public string PromotionCode { get; set; } = null!;

    public string PromotionName { get; set; } = null!;

    public string? PromotionDescription { get; set; }

    public string PromotionDiscountType { get; set; } = null!;

    public decimal PromotionDiscountValue { get; set; }

    public decimal? PromotionMaxDiscountAmount { get; set; }

    public bool IsStackable { get; set; }

    public int StackingPriority { get; set; }

    public int MaxUsagePerCustomer { get; set; }

    public int? PromotionUsageLimit { get; set; }

    public int PromotionUsageCount { get; set; }

    public DateTime PromotionStartAt { get; set; }

    public DateTime PromotionEndAt { get; set; }

    public string PromotionStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
