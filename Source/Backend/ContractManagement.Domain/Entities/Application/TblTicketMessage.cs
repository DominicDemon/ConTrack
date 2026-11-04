namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public partial class TblTicketMessage
{
    public long TicketMessageId { get; set; }
    public long TicketSupportId { get; set; }
    public long? TicketMessageSenderCustomerId { get; set; }
    public long? TicketMessageSenderEmployeeId { get; set; }
    public string? TicketMessageContent { get; set; }
    public string? TicketMessageType { get; set; } = "TEXT";
    public string? TicketMessageAttachmentUrl { get; set; }
    public string? TicketMessageAttachmentName { get; set; }
    public string? TicketMessageAttachmentType { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public TblTicketSupport? TicketSupport { get; set; }
}

