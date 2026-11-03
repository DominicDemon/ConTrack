using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Customer
{
    public long CustomerId { get; set; }

    public string? CustomerEmail { get; set; }

    public string CustomerPersonalPhone { get; set; } = null!;

    public string CustomerPasswordHash { get; set; } = null!;

    public string CustomerDisplayName { get; set; } = null!;

    public string? CustomerPersonalTaxIdentifier { get; set; }

    public int CustomerStatus { get; set; }

    public DateOnly? CustomerDob { get; set; }

    public string? CustomerGender { get; set; }

    public string? CustomerAddress { get; set; }

    public string? CustomerFirstname { get; set; }

    public string? CustomerMiddlename { get; set; }

    public string? CustomerLastname { get; set; }

    public DateTime? CustomerEmailVerifiedAt { get; set; }

    public DateTime? CustomerPersonalPhoneVerifiedAt { get; set; }

    public DateTime? CustomerLastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
