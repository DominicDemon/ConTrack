using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

[Table("TICKET_SUPPORT")]
public class TICKET_SUPPORT
{
    [Key]
    public long ticket_support_id { get; set; }
    public long? customer_id { get; set; }
    public long? department_id { get; set; }
    public long? tenant_id { get; set; }
    public long? product_id { get; set; }
    public long? service_id { get; set; }
    public string? ticket_code { get; set; }
    public string? ticket_title { get; set; }
    public string? ticket_description { get; set; }
    public string? ticket_priority { get; set; }
    public string? ticket_status { get; set; }
    public string? ticket_resolution { get; set; }
    public DateTime? ticket_resolved_at { get; set; }
    public DateTime? ticket_closed_at { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime? updated_at { get; set; }
}

