using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SystemPermissionGroup
{
    public long SystemPermissionGroupId { get; set; }

    public string SystemPermissionGroupCode { get; set; } = null!;

    public string SystemPermissionGroupName { get; set; } = null!;

    public string? SystemPermissionGroupDescription { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
