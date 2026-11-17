using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SAAS_PERMISSION_GROUP
{
    public long permission_group_id { get; set; }

    public long saas_type_id { get; set; }

    public string group_code { get; set; } = null!;

    public string group_name { get; set; } = null!;

    public string? group_description { get; set; }

    public string? group_icon { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
