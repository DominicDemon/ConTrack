using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_TYPE
{
    public int contract_type_id { get; set; }

    public string contract_type_code { get; set; } = null!;

    public string contract_type_name { get; set; } = null!;

    public string? contract_type_nameen { get; set; }

    public string? contract_type_description { get; set; }

    public string? contract_status { get; set; }

    public int? default_duration_months { get; set; }

    public DateTime created_date { get; set; }

    public DateTime? updated_date { get; set; }

    public int? created_employee_id { get; set; }

    public int? modified_employee_id { get; set; }
}
