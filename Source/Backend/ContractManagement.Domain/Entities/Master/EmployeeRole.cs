using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class EmployeeRole
{
    public long EmployeeRoleId { get; set; }

    public long EmployeeId { get; set; }

    public long SystemRoleId { get; set; }

    public DateTime SystemMemberRoleAssignedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
