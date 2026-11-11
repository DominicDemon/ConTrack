using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SAAS_PERMISSION_DEPENDENCY
{
    public long dependency_id { get; set; }

    public long permission_id { get; set; }

    public long depends_on_permission_id { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
