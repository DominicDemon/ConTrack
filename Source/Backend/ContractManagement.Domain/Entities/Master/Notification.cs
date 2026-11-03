using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Notification
{
    public long NotificationId { get; set; }

    public string NotificationEventkey { get; set; } = null!;

    public string NotificationModulecode { get; set; } = null!;

    public string NotificationEventcode { get; set; } = null!;

    public string NotificationSubjecttype { get; set; } = null!;

    public long NotificationSubjectid { get; set; }

    public string NotificationTitle { get; set; } = null!;

    public string NotificationBody { get; set; } = null!;

    public string? NotificationActionkey { get; set; }

    public long? EmployeeId { get; set; }

    public DateTime NotificationCreatedatutc { get; set; }
}
