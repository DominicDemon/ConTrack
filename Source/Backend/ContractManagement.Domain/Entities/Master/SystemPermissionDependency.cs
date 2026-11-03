using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SystemPermissionDependency
{
    public long SystemPermissionDependencyId { get; set; }

    public long SystemPermissionId { get; set; }

    public long DependsOnSystemPermissionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
