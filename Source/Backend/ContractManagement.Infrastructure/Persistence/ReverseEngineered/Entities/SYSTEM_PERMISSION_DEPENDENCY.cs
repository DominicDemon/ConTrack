using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SYSTEM_PERMISSION_DEPENDENCY
{
    public long system_permission_dependency_id { get; set; }

    public long system_permission_id { get; set; }

    public long depends_on_system_permission_id { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
