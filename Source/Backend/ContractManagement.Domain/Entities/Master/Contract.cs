using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Contract
{
    public long ContractId { get; set; }

    public int? ContractTypeId { get; set; }

    public string ContractCode { get; set; } = null!;

    public string ContractName { get; set; } = null!;

    public string? ContractNameen { get; set; }

    public long? PartnerId { get; set; }

    public long? EmployeeId { get; set; }

    public int? DepartmentId { get; set; }

    public string? SourceType { get; set; }

    public string? ContractStatus { get; set; }

    public string? ApprovalStatus { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public string? Currency { get; set; }

    public double? ContractValue { get; set; }

    public string? PaymentTerms { get; set; }

    public string? ContractDescription { get; set; }

    public long? FileStorageFileid { get; set; }

    public int? DefaultDurationMonths { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? CreatedEmployeeId { get; set; }

    public int? ModifiedEmployeeId { get; set; }
}
