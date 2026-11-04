using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class CustomerBusiness
{
    public long CustomerBusinessId { get; set; }

    public long CustomerId { get; set; }

    public string CustomerBusinessLegalName { get; set; } = null!;

    public string CustomerBusinessCompanyTaxIdentifier { get; set; } = null!;

    public string? CustomerBusinessPhone { get; set; }

    public string? CustomerBusinessEmail { get; set; }

    public string? CustomerBusinessOfficeAddress { get; set; }

    public string? CustomerBusinessHeadOfficeAddress { get; set; }

    public string? CustomerBusinessType { get; set; }

    public string? CustomerBusinessRepresentativeName { get; set; }

    public DateOnly? CustomerBusinessOperationStartDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
