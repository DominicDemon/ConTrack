using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Tenant
{
    public long TenantId { get; set; }

    public long? CustomerId { get; set; }

    public long SaasTypeId { get; set; }

    public long PackageLevelId { get; set; }

    public string TenantCode { get; set; } = null!;

    public string TenantName { get; set; } = null!;

    public string TenantSubdomain { get; set; } = null!;

    public string TenantStatus { get; set; } = null!;

    public DateTime? TenantExpiredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
