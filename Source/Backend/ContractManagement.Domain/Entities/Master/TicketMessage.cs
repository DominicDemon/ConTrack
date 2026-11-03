using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class TicketMessage
{
    public long TicketMessageId { get; set; }

    public long? TicketSupportId { get; set; }

    public long? TicketMessageSenderCustomerId { get; set; }

    public long? TicketMessageSenderEmployeeId { get; set; }

    public string? TicketMessageContent { get; set; }

    public string? TicketMessageType { get; set; }

    public DateTime? CreatedAt { get; set; }
}
