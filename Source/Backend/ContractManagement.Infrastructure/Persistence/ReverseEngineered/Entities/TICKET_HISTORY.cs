using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TICKET_HISTORY
{
    public long ticket_history_id { get; set; }

    public long? ticket_support_id { get; set; }

    public int? employee_id_start { get; set; }

    public int? employee_id_finished { get; set; }

    public int? changed_by_user_id { get; set; }

    public string? notes { get; set; }

    public string? old_status { get; set; }

    public string? new_status { get; set; }

    public DateTime? changed_at { get; set; }
}
