using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SaasPermissionDependency
{
    public long DependencyId { get; set; }

    public long PermissionId { get; set; }

    public long DependsOnPermissionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
