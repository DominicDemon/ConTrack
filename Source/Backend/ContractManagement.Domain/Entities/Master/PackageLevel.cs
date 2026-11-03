using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class PackageLevel
{
    public long PackageLevelId { get; set; }

    public long SaasTypeId { get; set; }

    public string PackageLevelCode { get; set; } = null!;

    public string PackageLevelName { get; set; } = null!;

    public string? PackageLevelDescription { get; set; }

    public string PackageLevelStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
