using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SERVICE_TYPE
{
    public int servicetype_id { get; set; }

    public string servicetype_name { get; set; } = null!;
}
