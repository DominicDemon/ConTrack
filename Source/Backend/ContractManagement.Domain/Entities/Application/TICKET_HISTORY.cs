using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

[Table("TICKET_HISTORY")]
public class TICKET_HISTORY
{
    [Key]
    public long ticket_history_id { get; set; }
    public long? ticket_support_id { get; set; }
    public int? employee_id_start { get; set; }
    public int? employee_id_finished { get; set; }
    public int? changed_by_user_id { get; set; }
    public string? notes { get; set; }
    public string? old_status { get; set; }
    public string? new_status { get; set; }
    public DateTime? changed_at { get; set; } = DateTime.UtcNow;
}

