using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractType
{
    public int ContractTypeId { get; set; }

    public string ContractTypeCode { get; set; } = null!;

    public string ContractTypeName { get; set; } = null!;

    public string? ContractTypeNameen { get; set; }

    public string? ContractTypeDescription { get; set; }

    public string? ContractStatus { get; set; }

    public int? DefaultDurationMonths { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? CreatedEmployeeId { get; set; }

    public int? ModifiedEmployeeId { get; set; }
}
