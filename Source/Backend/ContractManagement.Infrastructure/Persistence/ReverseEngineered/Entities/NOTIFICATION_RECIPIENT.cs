using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class NOTIFICATION_RECIPIENT
{
    public long notification_recipient_id { get; set; }

    public long notification_id { get; set; }

    public long employee_id { get; set; }

    public DateTime notification_recipient_createdatutc { get; set; }

    public DateTime? notification_recipient_readatutc { get; set; }

    public DateTime? notification_recipient_dismissedatutc { get; set; }
}
