namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public partial class TblTicketSupport
{
    public long TicketSupportId { get; set; }
    public long? CustomerId { get; set; }
    public long? TenantId { get; set; }
    public long? DepartmentId { get; set; }
    public long? ProductId { get; set; }
    public long? ServiceId { get; set; }
    public string? TicketCode { get; set; }
    public string? TicketTitle { get; set; }
    public string? TicketDescription { get; set; }
    public string? TicketPriority { get; set; }
    public string? TicketStatus { get; set; }
    public string? TicketResolution { get; set; }
    public DateTime? TicketResolvedAt { get; set; }
    public DateTime? TicketClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TblTicketMessage> Messages { get; set; } = new List<TblTicketMessage>();
    public ICollection<TblTicketSupportEmployee> AssignedEmployees { get; set; } = new List<TblTicketSupportEmployee>();
}

