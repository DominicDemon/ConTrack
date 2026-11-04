namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public partial class TblTicketSupportEmployee
{
    public long TicketSupportEmployeeId { get; set; }
    public long TicketSupportId { get; set; }
    public long EmployeeId { get; set; }
    public string? TicketSupportEmployeeAssignmentRole { get; set; }
    public DateTime? TicketSupportEmployeeAssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TicketSupportEmployeeUnassignedAt { get; set; }
    public long? TicketSupportEmployeeAssignedByEmployeeId { get; set; }

    public TblTicketSupport? TicketSupport { get; set; }
}

