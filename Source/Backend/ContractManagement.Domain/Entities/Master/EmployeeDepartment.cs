using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class EmployeeDepartment
{
    public long EmployeeId { get; set; }

    public long DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
