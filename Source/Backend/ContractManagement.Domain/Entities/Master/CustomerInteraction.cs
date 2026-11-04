using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class CustomerInteraction
{
    public long CustomerInteractionId { get; set; }

    public long Customerid { get; set; }

    public long Employeeid { get; set; }

    public DateTime CustomerInteractionInteractionAt { get; set; }

    public string CustomerInteractionInteractiontype { get; set; } = null!;

    public string CustomerInteractionInteractionsubject { get; set; } = null!;

    public string? CustomerInteractionInteractioncontent { get; set; }

    public DateTime? CustomerInteractionNextfollowupdate { get; set; }
}
