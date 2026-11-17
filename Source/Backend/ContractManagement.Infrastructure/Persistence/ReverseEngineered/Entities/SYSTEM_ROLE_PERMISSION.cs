using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SYSTEM_ROLE_PERMISSION
{
    public long system_role_id { get; set; }

    public long system_permission_id { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
