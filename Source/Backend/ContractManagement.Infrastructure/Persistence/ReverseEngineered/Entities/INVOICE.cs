using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class INVOICE
{
    public long invoice_id { get; set; }

    public string invoice_code { get; set; } = null!;

    public long? contract_version_id { get; set; }

    public long tenant_id { get; set; }

    public long customer_id { get; set; }

    public long? customer_business_id { get; set; }

    public long plan_version_id { get; set; }

    public int subscription_days { get; set; }

    public bool is_tax_invoice_requested { get; set; }

    public string invoice_buyer_type { get; set; } = null!;

    public string invoice_billing_name { get; set; } = null!;

    public string? invoice_billing_tax_identifier { get; set; }

    public string? invoice_billing_address { get; set; }

    public string? invoice_billing_email { get; set; }

    public string? invoice_billing_phone { get; set; }

    public decimal invoice_subtotal { get; set; }

    public decimal invoice_discount_amount { get; set; }

    public decimal invoice_total_amount { get; set; }

    public string invoice_currency { get; set; } = null!;

    public string invoice_status { get; set; } = null!;

    public DateTime? invoice_issued_at { get; set; }

    public DateTime? invoice_paid_at { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
