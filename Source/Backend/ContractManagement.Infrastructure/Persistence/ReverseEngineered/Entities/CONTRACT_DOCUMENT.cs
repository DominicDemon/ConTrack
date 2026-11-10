using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_DOCUMENT
{
    public long contract_document_id { get; set; }

    public int? contract_document_current_version_id { get; set; }

    public long customer_id { get; set; }

    public long employee_id { get; set; }

    public string? tenant_id { get; set; }

    public long contract_template_id { get; set; }

    public string? contract_document_code { get; set; }

    public string contract_document_name { get; set; } = null!;

    public int contract_document_type { get; set; }

    public int contract_document_status { get; set; }

    public DateTime created_date { get; set; }

    public DateTime? updated_date { get; set; }

    public byte[] row_version { get; set; } = null!;
}
