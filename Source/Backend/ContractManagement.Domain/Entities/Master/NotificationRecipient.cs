using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class NotificationRecipient
{
    public long NotificationRecipientId { get; set; }

    public long NotificationId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime NotificationRecipientCreatedatutc { get; set; }

    public DateTime? NotificationRecipientReadatutc { get; set; }

    public DateTime? NotificationRecipientDismissedatutc { get; set; }
}
