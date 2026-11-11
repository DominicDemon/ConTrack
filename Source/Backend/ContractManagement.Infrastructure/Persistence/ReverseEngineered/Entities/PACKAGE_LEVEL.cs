using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PACKAGE_LEVEL
{
    public long package_level_id { get; set; }

    public long saas_type_id { get; set; }

    public string package_level_code { get; set; } = null!;

    public string package_level_name { get; set; } = null!;

    public string? package_level_description { get; set; }

    public string package_level_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
