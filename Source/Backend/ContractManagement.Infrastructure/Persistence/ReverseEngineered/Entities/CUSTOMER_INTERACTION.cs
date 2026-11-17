using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CUSTOMER_INTERACTION
{
    public long customer_interaction_id { get; set; }

    public long customerid { get; set; }

    public long employeeid { get; set; }

    public DateTime customer_interaction_interaction_at { get; set; }

    public string customer_interaction_interactiontype { get; set; } = null!;

    public string customer_interaction_interactionsubject { get; set; } = null!;

    public string? customer_interaction_interactioncontent { get; set; }

    public DateTime? customer_interaction_nextfollowupdate { get; set; }
}
