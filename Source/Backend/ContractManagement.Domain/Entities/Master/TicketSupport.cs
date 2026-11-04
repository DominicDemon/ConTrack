using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class TicketSupport
{
    public long TicketSupportId { get; set; }

    public long? CustomerId { get; set; }

    public long? TenantId { get; set; }

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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
