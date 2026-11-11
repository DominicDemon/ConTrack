using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_APPENDix
{
    public long contract_appendix_id { get; set; }

    public string? contract_appendix_code { get; set; }

    public string? contract_appendix_name { get; set; }

    public long contract_id { get; set; }

    public long contract_template_id { get; set; }

    public string? contract_appendix_description { get; set; }

    public DateTime? contract_appendix_signdate { get; set; }

    public DateTime? contract_appendix_effective_date { get; set; }

    public DateTime? contract_appendix_expire_date { get; set; }

    public long? empolyee_id { get; set; }

    public DateTime contract_appendix_created_date { get; set; }

    public byte[] contract_version_rowversion { get; set; } = null!;
}
