using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class PackageLevelPermission
{
    public long PackageLevelId { get; set; }

    public long PermissionId { get; set; }
}
