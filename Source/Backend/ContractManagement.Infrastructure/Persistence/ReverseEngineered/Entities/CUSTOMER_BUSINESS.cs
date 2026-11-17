using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CUSTOMER_BUSINESS
{
    public long customer_business_id { get; set; }

    public long customer_id { get; set; }

    public string customer_business_legal_name { get; set; } = null!;

    public string customer_business_company_tax_identifier { get; set; } = null!;

    public string? customer_business_phone { get; set; }

    public string? customer_business_email { get; set; }

    public string? customer_business_office_address { get; set; }

    public string? customer_business_head_office_address { get; set; }

    public string? customer_business_type { get; set; }

    public string? customer_business_representative_name { get; set; }

    public DateOnly? customer_business_operation_start_date { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
