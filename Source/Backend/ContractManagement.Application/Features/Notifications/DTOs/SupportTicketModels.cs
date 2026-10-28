namespace ContractManagement.API.Domains.Support;

public sealed class SupportTicketDto
{
    public int TicketSupportId { get; set; }
    public int? CustomerId { get; set; }
    public int? TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public int? CreatedByEmployeeId { get; set; }
    public string? CreatedByName { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string TicketSubject { get; set; } = string.Empty;
    public string TicketDescription { get; set; } = string.Empty;
    public string TicketPriority { get; set; } = "Medium";
    public string TicketStatus { get; set; } = "Open";
    public string Department { get; set; } = "KỸ THUẬT";
    public string? Module { get; set; }
    public string? TicketResolution { get; set; }
    public DateTime? TicketResolvedAt { get; set; }
    public DateTime? TicketClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public List<SupportTicketMessageDto> Messages { get; set; } = [];
}

public sealed class SupportTicketMessageDto
{
    public int TicketMessageId { get; set; }
    public int TicketSupportId { get; set; }
    public int? SenderCustomerId { get; set; }
    public int? SenderEmployeeId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsFromEmployee { get; set; }
    public string TicketMessageContent { get; set; } = string.Empty;
    public string TicketMessageType { get; set; } = "TEXT";
    public string? TicketMessageAttachmentUrl { get; set; }
    public string? TicketMessageAttachmentName { get; set; }
    public string? AttachmentUrl => TicketMessageAttachmentUrl;
    public string? AttachmentName => TicketMessageAttachmentName;
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminNotificationDto
{
    public int NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string SubjectType { get; set; } = "SupportTicket";
    public int SubjectId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string Department { get; set; } = "KỸ THUẬT";
    public string ActionKey { get; set; } = "new-ticket";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class CreateSupportTicketRequestDto
{
    public string? TicketSubject { get; set; }
    public string? Subject { get; set; }
    public string? TicketDescription { get; set; }
    public string? Description { get; set; }
    public string? TicketPriority { get; set; }
    public string? Priority { get; set; }
    public string? Department { get; set; }
    public string? Module { get; set; }
    public List<string>? AttachmentNames { get; set; }

    public string ResolvedSubject => (TicketSubject ?? Subject ?? string.Empty).Trim();
    public string ResolvedDescription => (TicketDescription ?? Description ?? string.Empty).Trim();
    public string? ResolvedPriority => !string.IsNullOrWhiteSpace(TicketPriority) ? TicketPriority : Priority;
}

public sealed class CustomerReplyTicketRequestDto
{
    public string? Content { get; set; }
    public string? MessageContent { get; set; }
    public string? AttachmentName { get; set; }
}

public sealed class AdminReplyTicketRequestDto
{
    public string MessageContent { get; set; } = string.Empty;
    public bool? IsInternalNote { get; set; }
}

public sealed class UpdateTicketStatusRequestDto
{
    public string Status { get; set; } = "Open";
    public string? Resolution { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
}

