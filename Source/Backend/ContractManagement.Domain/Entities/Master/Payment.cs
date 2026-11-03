using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Payment
{
    public long PaymentId { get; set; }

    public long InvoiceId { get; set; }

    public string PaymentReference { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string? PaymentProvider { get; set; }

    public string? PaymentProviderTransactionId { get; set; }

    public decimal PaymentAmount { get; set; }

    public string PaymentCurrency { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public DateTime? PaymentPaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
