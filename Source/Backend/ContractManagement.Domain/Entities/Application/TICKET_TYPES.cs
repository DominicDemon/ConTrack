using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

[Table("TICKET_TYPES")]
public class TICKET_TYPES
{
    [Key]
    public long ticket_type_id { get; set; }
    public string? ticket_name { get; set; }
}

