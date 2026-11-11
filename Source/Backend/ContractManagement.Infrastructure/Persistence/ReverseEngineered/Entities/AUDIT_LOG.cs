using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class AUDIT_LOG
{
    public long audit_log_id { get; set; }

    public string action_type { get; set; } = null!;

    public string entity_name { get; set; } = null!;

    public string entity_id { get; set; } = null!;

    public string actor_type { get; set; } = null!;

    public long? actor_id { get; set; }

    public string? actor_ip_address { get; set; }

    public string? old_data { get; set; }

    public string? new_data { get; set; }

    public DateTime created_at { get; set; }
}
