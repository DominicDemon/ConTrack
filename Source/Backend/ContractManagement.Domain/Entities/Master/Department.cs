using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Department
{
    public long DepartmentId { get; set; }

    public string DepartmentCode { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;

    public string? DepartmentDescription { get; set; }

    public string DepartmentStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    /// <summary>Role given to staff of this department; null when the department has a manager only.</summary>
    public long? DepartmentStaffRoleId { get; set; }

    public long? DepartmentManagerRoleId { get; set; }

    /// <summary>The single manager of this department (005-department-roles.sql).</summary>
    public long? DepartmentManagerEmployeeId { get; set; }
}
