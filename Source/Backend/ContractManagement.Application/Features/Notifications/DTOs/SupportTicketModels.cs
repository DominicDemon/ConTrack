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
    public int? DepartmentId { get; set; }
    public string Department { get; set; } = "KỸ THUẬT";
    public long? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public string? Module { get; set; }
    public string? TicketResolution { get; set; }
    public DateTime? TicketResolvedAt { get; set; }
    public DateTime? TicketClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int? DepartmentHeadEmployeeId { get; set; }
    public string? DepartmentHeadName { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public DateTime? AssignedAt { get; set; }

    /// <summary>
    /// Vai trò của người đang xem ticket: "CUSTOMER" | "DEPARTMENT_HEAD" | "STAFF" | "SYSTEM_ADMIN"
    /// </summary>
    public string ViewerRole { get; set; } = "CUSTOMER";
    public int? ViewerEmployeeId { get; set; }
    public int? ViewerDepartmentId { get; set; }

    public List<string> AttachmentUrls { get; set; } = [];
    public List<string> AttachmentNames { get; set; } = [];

    public List<TicketEmployeeAssignmentDto> Assignments { get; set; } = [];
    public List<SupportTicketMessageDto> Messages { get; set; } = [];
}

public sealed class TicketEmployeeAssignmentDto
{
    public long TicketSupportEmployeeId { get; set; }
    public long TicketSupportId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? EmployeeEmail { get; set; }
    public string AssignmentRole { get; set; } = string.Empty; // "DEPARTMENT_HEAD", "ASSIGNEE"
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public int? AssignedByEmployeeId { get; set; }
    public string? AssignedByName { get; set; }
}

public sealed class DepartmentEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? EmployeeEmail { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsManager { get; set; }
}

public sealed class SupportServiceItemDto
{
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
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
    public List<string> AttachmentUrls { get; set; } = [];
    public List<string> AttachmentNames { get; set; } = [];
    public string? AttachmentUrl => TicketMessageAttachmentUrl ?? AttachmentUrls.FirstOrDefault();
    public string? AttachmentName => TicketMessageAttachmentName ?? AttachmentNames.FirstOrDefault();
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
    public int? DepartmentId { get; set; }
    public string Department { get; set; } = "KỸ THUẬT";
    public int? TargetEmployeeId { get; set; }
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
    public int? DepartmentId { get; set; }
    public string? Department { get; set; }
    public string? Module { get; set; }
    public long? ProductId { get; set; }
    public long? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public List<string>? AttachmentNames { get; set; }
    public List<string>? AttachmentUrls { get; set; }

    public string ResolvedSubject => (TicketSubject ?? Subject ?? string.Empty).Trim();
    public string ResolvedDescription => (TicketDescription ?? Description ?? string.Empty).Trim();
    public string? ResolvedPriority => !string.IsNullOrWhiteSpace(TicketPriority) ? TicketPriority : Priority;
}

public sealed class AssignTicketToStaffRequestDto
{
    public int StaffEmployeeId { get; set; }
    public string? Priority { get; set; }
    public string? Note { get; set; }
}

public sealed class StaffCompleteTicketRequestDto
{
    public string Resolution { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class HeadResolveTicketRequestDto
{
    public string Resolution { get; set; } = string.Empty;
    public string? MessageToCustomer { get; set; }
    public bool CloseTicket { get; set; } = true;
}

public sealed class CustomerReplyTicketRequestDto
{
    public string? Content { get; set; }
    public string? MessageContent { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentUrl { get; set; }
    public List<string>? AttachmentNames { get; set; }
    public List<string>? AttachmentUrls { get; set; }
    public string? AttachmentType { get; set; }
}

public sealed class AdminReplyTicketRequestDto
{
    public string? MessageContent { get; set; }
    public bool? IsInternalNote { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentUrl { get; set; }
    public List<string>? AttachmentNames { get; set; }
    public List<string>? AttachmentUrls { get; set; }
}

public sealed class UpdateTicketStatusRequestDto
{
    public string Status { get; set; } = "Open";
    public string? Resolution { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
}
