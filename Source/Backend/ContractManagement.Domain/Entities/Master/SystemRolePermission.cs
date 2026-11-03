using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SystemRolePermission
{
    public long SystemRoleId { get; set; }

    public long SystemPermissionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
