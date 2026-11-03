using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class TenantDatabase
{
    public long TenantDatabaseId { get; set; }

    public long TenantId { get; set; }

    public string TenantDatabaseProvider { get; set; } = null!;

    public string TenantDatabaseHost { get; set; } = null!;

    public int TenantDatabasePort { get; set; }

    public string TenantDatabaseDbName { get; set; } = null!;

    public string TenantDatabaseUser { get; set; } = null!;

    public string? TenantDatabasePasswordEncrypted { get; set; }

    public string TenantDatabaseProvisioningStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
