using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class PlanVersion
{
    public long PlanVersionId { get; set; }

    public long PlanId { get; set; }

    public int PlanVersionNumber { get; set; }

    public int PlanVersionDurationDays { get; set; }

    public decimal PlanVersionPrice { get; set; }

    public string PlanVersionCurrency { get; set; } = null!;

    public DateTime PlanVersionEffectiveFrom { get; set; }

    public DateTime? PlanVersionEffectiveTo { get; set; }

    public string PlanVersionStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
