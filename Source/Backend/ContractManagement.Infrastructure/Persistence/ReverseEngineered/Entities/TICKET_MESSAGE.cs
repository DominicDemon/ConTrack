using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TICKET_MESSAGE
{
    public long ticket_message_id { get; set; }

    public long? ticket_support_id { get; set; }

    public long? ticket_message_sender_customer_id { get; set; }

    public long? ticket_message_sender_employee_id { get; set; }

    public string? ticket_message_content { get; set; }

    public string? ticket_message_type { get; set; }

    public DateTime? created_at { get; set; }
}
