using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Plan
{
    public long PlanId { get; set; }

    public long SaasTypeId { get; set; }

    public long PackageLevelId { get; set; }

    public string PlanCode { get; set; } = null!;

    public string PlanName { get; set; } = null!;

    public long? CurrentActiveVersionId { get; set; }

    public string PlanStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
