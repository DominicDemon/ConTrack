using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TICKET_TYPE
{
    public long ticket_type_id { get; set; }

    public string? ticket_name { get; set; }
}
