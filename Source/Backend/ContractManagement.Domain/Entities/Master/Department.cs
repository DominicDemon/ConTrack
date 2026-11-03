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
}
