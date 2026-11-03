using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Employee
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string? EmployeeEmail { get; set; }

    public string? EmployeePhone { get; set; }

    public string EmployeePasswordHash { get; set; } = null!;

    public string EmployeeDisplayName { get; set; } = null!;

    public string EmployeeUsername { get; set; } = null!;

    public string? EmployeePersonalIdentifier { get; set; }

    public string EmployeeStatus { get; set; } = null!;

    public DateOnly? EmployeeDob { get; set; }

    public string? EmployeeGender { get; set; }

    public string? EmployeeAddress { get; set; }

    public string? EmployeeFirstName { get; set; }

    public string? EmployeeMiddleName { get; set; }

    public string? EmployeeLastName { get; set; }

    public DateTime? EmployeeEmailVerifiedAt { get; set; }

    public DateTime? EmployeeHandPhoneVerifiedAt { get; set; }

    public DateTime? EmployeeLastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
