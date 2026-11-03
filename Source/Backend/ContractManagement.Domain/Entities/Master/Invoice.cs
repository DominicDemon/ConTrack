using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Invoice
{
    public long InvoiceId { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public long? ContractVersionId { get; set; }

    public long TenantId { get; set; }

    public long CustomerId { get; set; }

    public long? CustomerBusinessId { get; set; }

    public long PlanVersionId { get; set; }

    public int SubscriptionDays { get; set; }

    public bool IsTaxInvoiceRequested { get; set; }

    public string InvoiceBuyerType { get; set; } = null!;

    public string InvoiceBillingName { get; set; } = null!;

    public string? InvoiceBillingTaxIdentifier { get; set; }

    public string? InvoiceBillingAddress { get; set; }

    public string? InvoiceBillingEmail { get; set; }

    public string? InvoiceBillingPhone { get; set; }

    public decimal InvoiceSubtotal { get; set; }

    public decimal InvoiceDiscountAmount { get; set; }

    public decimal InvoiceTotalAmount { get; set; }

    public string InvoiceCurrency { get; set; } = null!;

    public string InvoiceStatus { get; set; } = null!;

    public DateTime? InvoiceIssuedAt { get; set; }

    public DateTime? InvoicePaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
