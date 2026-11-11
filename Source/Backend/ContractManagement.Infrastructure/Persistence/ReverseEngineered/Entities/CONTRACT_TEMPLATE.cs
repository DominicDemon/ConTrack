using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_TEMPLATE
{
    public long contract_template_id { get; set; }

    public string contract_template_code { get; set; } = null!;

    public string contract_template_name { get; set; } = null!;

    public string? contract_template_nameen { get; set; }

    public string? contract_template_description { get; set; }

    public int contract_template_document_type { get; set; }

    public int contract_template_language_mode { get; set; }

    public bool is_active { get; set; }

    public DateTime created_date { get; set; }

    public DateTime? updated_date { get; set; }

    public int? created_employee_id { get; set; }

    public int? modified_employee_id { get; set; }
}
