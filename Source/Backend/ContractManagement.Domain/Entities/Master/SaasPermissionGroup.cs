using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SaasPermissionGroup
{
    public long PermissionGroupId { get; set; }

    public long SaasTypeId { get; set; }

    public string GroupCode { get; set; } = null!;

    public string GroupName { get; set; } = null!;

    public string? GroupDescription { get; set; }

    public string? GroupIcon { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
