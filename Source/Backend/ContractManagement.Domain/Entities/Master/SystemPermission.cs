using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SystemPermission
{
    public long SystemPermissionId { get; set; }

    public long SystemPermissionGroupId { get; set; }

    public string SystemPermissionCode { get; set; } = null!;

    public string SystemPermissionName { get; set; } = null!;

    public string? SystemPermissionResource { get; set; }

    public string? SystemPermissionAction { get; set; }

    public string? SystemPermissionDescription { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
