using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

[Table("TICKET_ATTACHMENTS")]
public class TICKET_ATTACHMENTS
{
    [Key]
    public long ticket_attachment_id { get; set; }
    public long? ticket_message_id { get; set; }
    public string? file_name_1 { get; set; }
    public string? file_name_2 { get; set; }
    public string? file_name_3 { get; set; }
    public int? file_size { get; set; }
    public string? mime_type { get; set; }
    public DateTime? uploaded_at { get; set; } = DateTime.UtcNow;
}

