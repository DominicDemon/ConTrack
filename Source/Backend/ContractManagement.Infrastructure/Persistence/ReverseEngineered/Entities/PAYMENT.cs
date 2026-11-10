using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PAYMENT
{
    public long payment_id { get; set; }

    public long invoice_id { get; set; }

    public string payment_reference { get; set; } = null!;

    public string payment_method { get; set; } = null!;

    public string? payment_provider { get; set; }

    public string? payment_provider_transaction_id { get; set; }

    public decimal payment_amount { get; set; }

    public string payment_currency { get; set; } = null!;

    public string payment_status { get; set; } = null!;

    public DateTime? payment_paid_at { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
