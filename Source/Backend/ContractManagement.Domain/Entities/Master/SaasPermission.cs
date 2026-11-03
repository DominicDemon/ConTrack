using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SaasPermission
{
    public long PermissionId { get; set; }

    public long PermissionGroupId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public string PermissionName { get; set; } = null!;

    public string? PermissionDescription { get; set; }

    public int PermissionPoint { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
