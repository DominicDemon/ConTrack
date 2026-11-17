using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TICKET_ATTACHMENT
{
    public long ticket_attachment_id { get; set; }

    public long? ticket_message_id { get; set; }

    public string? file_name_1 { get; set; }

    public string? file_name_2 { get; set; }

    public string? file_name_3 { get; set; }

    public int? file_size { get; set; }

    public string? mime_type { get; set; }

    public DateTime? uploaded_at { get; set; }
}
