using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class NOTIFICATION
{
    public long notification_id { get; set; }

    public string notification_eventkey { get; set; } = null!;

    public string notification_modulecode { get; set; } = null!;

    public string notification_eventcode { get; set; } = null!;

    public string notification_subjecttype { get; set; } = null!;

    public long notification_subjectid { get; set; }

    public string notification_title { get; set; } = null!;

    public string notification_body { get; set; } = null!;

    public string? notification_actionkey { get; set; }

    public long? employee_id { get; set; }

    public DateTime notification_createdatutc { get; set; }
}
