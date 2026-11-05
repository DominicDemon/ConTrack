using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

[Table("TICKET_SUPPORT_EMPLOYEE")]
public class TICKET_SUPPORT_EMPLOYEE
{
    [Key]
    public long ticket_support_employee_id { get; set; }
    public long? ticket_support_id { get; set; }
    public long? employee_id { get; set; }
    public string? ticket_support_employee_assignment_role { get; set; }
    public DateTime? ticket_support_employee_assigned_at { get; set; } = DateTime.UtcNow;
    public DateTime? ticket_support_employee_unassigned_at { get; set; }
    public long? ticket_support_employee_assigned_by_employee_id { get; set; }
}

