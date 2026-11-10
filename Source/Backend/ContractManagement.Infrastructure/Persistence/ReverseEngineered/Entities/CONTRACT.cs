using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT
{
    public long contract_id { get; set; }

    public int? contract_type_id { get; set; }

    public string contract_code { get; set; } = null!;

    public string contract_name { get; set; } = null!;

    public string? contract_nameen { get; set; }

    public long? partner_id { get; set; }

    public long? employee_id { get; set; }

    public int? department_id { get; set; }

    public string? source_type { get; set; }

    public string? contract_status { get; set; }

    public string? approval_status { get; set; }

    public DateTime? signed_date { get; set; }

    public DateTime? effective_date { get; set; }

    public DateTime? expiration_date { get; set; }

    public string? currency { get; set; }

    public double? contract_value { get; set; }

    public string? payment_terms { get; set; }

    public string? contract_description { get; set; }

    public long? file_storage_fileid { get; set; }

    public int? default_duration_months { get; set; }

    public DateTime created_date { get; set; }

    public DateTime? updated_date { get; set; }

    public int? created_employee_id { get; set; }

    public int? modified_employee_id { get; set; }
}
