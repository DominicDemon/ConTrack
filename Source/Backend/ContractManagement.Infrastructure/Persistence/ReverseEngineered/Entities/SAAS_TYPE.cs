using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SAAS_TYPE
{
    public long saas_type_id { get; set; }

    public long product_id { get; set; }

    public string saas_type_code { get; set; } = null!;

    public string saas_type_name { get; set; } = null!;

    public string? saas_type_description { get; set; }

    public string saas_type_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
