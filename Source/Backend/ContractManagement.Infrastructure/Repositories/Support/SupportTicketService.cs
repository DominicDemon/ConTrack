using System.Text.Json;
using ContractManagement.API.Domains.Email;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Support;

public interface ISupportTicketService
{
    Task<IReadOnlyList<SupportTicketDto>> GetAllForAdminAsync(
        string? status,
        string? priority,
        string? department,
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketDto>> GetAllForTenantAsync(
        int tenantId,
        string tenantCode,
        int employeeId,
        string? status,
        string? priority,
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> GetByIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> GetByIdForCustomerAsync(
        int ticketId,
        int tenantId,
        string tenantCode,
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto> CreateCustomerTicketAsync(
        int tenantId,
        string tenantCode,
        string tenantName,
        int employeeId,
        string senderName,
        CreateSupportTicketRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketMessageDto?> AddCustomerMessageAsync(
        int ticketId,
        int tenantId,
        string tenantCode,
        string tenantName,
        int employeeId,
        string senderName,
        CustomerReplyTicketRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketMessageDto?> AddAdminMessageAsync(
        int ticketId,
        int systemAdminId,
        string adminFullName,
        AdminReplyTicketRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> UpdateTicketStatusAsync(
        int ticketId,
        int systemAdminId,
        string adminFullName,
        UpdateTicketStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminNotificationDto> Items, int UnreadCount)> GetAdminNotificationsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> GetAdminUnreadNotificationCountAsync(
        CancellationToken cancellationToken = default);

    Task<bool> MarkAdminNotificationReadAsync(
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAdminNotificationsReadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SupportTicketStoreState
{
    public int NextTicketId { get; set; } = 5;
    public int NextMessageId { get; set; } = 500;
    public int NextNotificationId { get; set; } = 10;
    public List<SupportTicketDto> Tickets { get; set; } = [];
    public List<AdminNotificationDto> AdminNotifications { get; set; } = [];
}

public sealed class SupportTicketService : ISupportTicketService
{
    private static readonly SemaphoreSlim StoreLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static SupportTicketStoreState? _cachedState;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailTransport _emailTransport;
    private readonly IOptions<SmtpEmailOptions> _smtpOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupportTicketService> _logger;
    private readonly string _storeFilePath;

    public SupportTicketService(
        IServiceScopeFactory scopeFactory,
        IEmailTransport emailTransport,
        IOptions<SmtpEmailOptions> smtpOptions,
        IOptions<PrivateFileStorageOptions> storageOptions,
        IConfiguration configuration,
        ILogger<SupportTicketService> logger)
    {
        _scopeFactory = scopeFactory;
        _emailTransport = emailTransport;
        _smtpOptions = smtpOptions;
        _configuration = configuration;
        _logger = logger;

        var rootPath = storageOptions.Value.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = Path.Combine(AppContext.BaseDirectory, "App_Data");
        }
        else if (!Path.IsPathRooted(rootPath))
        {
            rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootPath));
        }

        var centralDir = Path.Combine(rootPath, "central");
        Directory.CreateDirectory(centralDir);
        _storeFilePath = Path.Combine(centralDir, "support-tickets-store.json");
    }

    public async Task<IReadOnlyList<SupportTicketDto>> GetAllForAdminAsync(
        string? status,
        string? priority,
        string? department,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            IEnumerable<SupportTicketDto> query = state.Tickets
                .OrderByDescending(t => t.CreatedAt);

            query = ApplyFilters(query, status, priority, department, keyword);
            return query.Select(CloneTicket).ToList();
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<IReadOnlyList<SupportTicketDto>> GetAllForTenantAsync(
        int tenantId,
        string tenantCode,
        int employeeId,
        string? status,
        string? priority,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);

            // Khách hàng chỉ được xem các yêu cầu hỗ trợ do chính mình / tổ chức của mình gửi
            IEnumerable<SupportTicketDto> query = state.Tickets
                .Where(t => BelongsToCustomer(t, tenantId, tenantCode, employeeId))
                .OrderByDescending(t => t.CreatedAt);

            query = ApplyFilters(query, status, priority, null, keyword);
            return query.Select(CloneTicket).ToList();
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> GetByIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            return ticket is null ? null : CloneTicket(ticket);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> GetByIdForCustomerAsync(
        int ticketId,
        int tenantId,
        string tenantCode,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t =>
                t.TicketSupportId == ticketId
                && BelongsToCustomer(t, tenantId, tenantCode, employeeId));
            return ticket is null ? null : CloneTicket(ticket);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    private static bool BelongsToCustomer(
        SupportTicketDto ticket,
        int tenantId,
        string tenantCode,
        int employeeId)
    {
        var sameTenant = (tenantId > 0 && ticket.TenantId == tenantId)
            || (!string.IsNullOrWhiteSpace(tenantCode)
                && string.Equals(ticket.TenantCode, tenantCode, StringComparison.OrdinalIgnoreCase));

        if (!sameTenant)
        {
            return false;
        }

        if (employeeId > 0)
        {
            return ticket.CreatedByEmployeeId == employeeId || ticket.CustomerId == employeeId;
        }

        return true;
    }

    public async Task<SupportTicketDto> CreateCustomerTicketAsync(
        int tenantId,
        string tenantCode,
        string tenantName,
        int employeeId,
        string senderName,
        CreateSupportTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        SupportTicketDto createdTicket;
        string? attachmentSummary = request.AttachmentNames is { Count: > 0 }
            ? string.Join(", ", request.AttachmentNames.Where(x => !string.IsNullOrWhiteSpace(x)))
            : null;

        var senderDisplay = string.IsNullOrWhiteSpace(senderName)
            ? $"{tenantName} ({tenantCode})"
            : $"{senderName} - {tenantName}";

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var ticketId = state.NextTicketId++;
            var msgId = state.NextMessageId++;
            var notifId = state.NextNotificationId++;

            var randomSuffix = now.Ticks.ToString()[^6..];
            var ticketCode = $"TK-{now:MMdd}{randomSuffix}";
            var priority = NormalizePriority(request.ResolvedPriority);
            var department = string.IsNullOrWhiteSpace(request.Department)
                ? "KỸ THUẬT"
                : request.Department.Trim();
            var module = string.IsNullOrWhiteSpace(request.Module)
                ? "Ký số & Hợp đồng điện tử"
                : request.Module.Trim();
            var subjectText = request.ResolvedSubject;
            var descriptionText = request.ResolvedDescription;

            var initialMessage = new SupportTicketMessageDto
            {
                TicketMessageId = msgId,
                TicketSupportId = ticketId,
                SenderCustomerId = employeeId,
                SenderName = senderDisplay,
                IsFromEmployee = false,
                TicketMessageContent = descriptionText,
                TicketMessageType = "TEXT",
                TicketMessageAttachmentName = attachmentSummary,
                CreatedAt = now
            };

            createdTicket = new SupportTicketDto
            {
                TicketSupportId = ticketId,
                CustomerId = employeeId,
                TenantId = tenantId,
                TenantCode = tenantCode,
                TenantName = tenantName,
                CreatedByEmployeeId = employeeId,
                CreatedByName = senderName,
                TicketCode = ticketCode,
                TicketSubject = subjectText,
                TicketDescription = descriptionText,
                TicketPriority = priority,
                TicketStatus = "Open",
                Department = department,
                Module = module,
                CreatedAt = now,
                UpdatedAt = now,
                Messages = [initialMessage]
            };

            state.Tickets.Insert(0, createdTicket);

            var priorityLabel = SupportTicketEmailTemplate.GetPriorityLabel(priority);
            var adminNotification = new AdminNotificationDto
            {
                NotificationId = notifId,
                Title = $"Yêu cầu hỗ trợ mới [{ticketCode}]",
                Body = $"{tenantName} ({tenantCode}) gửi yêu cầu [{priorityLabel}] tới {department}: {createdTicket.TicketSubject}",
                SubjectType = "SupportTicket",
                SubjectId = ticketId,
                TicketCode = ticketCode,
                TenantName = tenantName,
                TenantCode = tenantCode,
                Priority = priority,
                Department = department,
                ActionKey = "new-ticket",
                CreatedAtUtc = now,
                ReadAtUtc = null
            };

            state.AdminNotifications.Insert(0, adminNotification);
            await SaveStateAsync(state, cancellationToken);
            createdTicket = CloneTicket(createdTicket);
        }
        finally
        {
            StoreLock.Release();
        }

        // Gửi email thông báo về email của người xử lý hỗ trợ (SystemAdmin)
        _ = Task.Run(() => SendNewTicketEmailsSafeAsync(createdTicket, senderDisplay, attachmentSummary));

        return createdTicket;
    }

    public async Task<SupportTicketMessageDto?> AddCustomerMessageAsync(
        int ticketId,
        int tenantId,
        string tenantCode,
        string tenantName,
        int employeeId,
        string senderName,
        CustomerReplyTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var content = (request.MessageContent ?? request.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Nội dung phản hồi không được để trống.");
        }

        SupportTicketDto snapshotTicket;
        SupportTicketMessageDto newMessage;

        var senderDisplay = string.IsNullOrWhiteSpace(senderName)
            ? $"{tenantName} ({tenantCode})"
            : $"{senderName} ({tenantCode})";

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t =>
                t.TicketSupportId == ticketId
                && BelongsToCustomer(t, tenantId, tenantCode, employeeId));
            if (ticket is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            newMessage = new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderCustomerId = employeeId,
                SenderName = senderDisplay,
                IsFromEmployee = false,
                TicketMessageContent = content,
                TicketMessageType = "TEXT",
                TicketMessageAttachmentName = string.IsNullOrWhiteSpace(request.AttachmentName)
                    ? null
                    : request.AttachmentName.Trim(),
                CreatedAt = now
            };

            ticket.Messages.Add(newMessage);
            ticket.UpdatedAt = now;
            if (string.Equals(ticket.TicketStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                ticket.TicketStatus = "In_Progress";
            }

            var notif = new AdminNotificationDto
            {
                NotificationId = state.NextNotificationId++,
                Title = $"Phản hồi mới từ khách hàng [{ticket.TicketCode}]",
                Body = $"{senderDisplay}: {(content.Length > 100 ? content[..100] + "..." : content)}",
                SubjectType = "SupportTicket",
                SubjectId = ticket.TicketSupportId,
                TicketCode = ticket.TicketCode,
                TenantName = ticket.TenantName,
                TenantCode = ticket.TenantCode,
                Priority = ticket.TicketPriority,
                Department = ticket.Department,
                ActionKey = "customer-reply",
                CreatedAtUtc = now,
                ReadAtUtc = null
            };

            state.AdminNotifications.Insert(0, notif);
            await SaveStateAsync(state, cancellationToken);
            snapshotTicket = CloneTicket(ticket);
        }
        finally
        {
            StoreLock.Release();
        }

        // Gửi email thông báo cho người xử lý hỗ trợ khi khách hàng phản hồi
        _ = Task.Run(() => SendCustomerReplyEmailsSafeAsync(snapshotTicket, newMessage));

        return newMessage;
    }

    public async Task<SupportTicketMessageDto?> AddAdminMessageAsync(
        int ticketId,
        int systemAdminId,
        string adminFullName,
        AdminReplyTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var content = (request.MessageContent ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Nội dung phản hồi không được để trống.");
        }

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var displayName = string.IsNullOrWhiteSpace(adminFullName)
                ? "Chuyên viên Hỗ trợ ConTrack"
                : adminFullName.Trim();

            var newMsg = new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderEmployeeId = systemAdminId,
                SenderName = displayName,
                IsFromEmployee = true,
                TicketMessageContent = content,
                TicketMessageType = "TEXT",
                CreatedAt = now
            };

            ticket.Messages.Add(newMsg);
            ticket.UpdatedAt = now;
            if (string.IsNullOrWhiteSpace(ticket.AssignedEmployeeName))
            {
                ticket.AssignedEmployeeId = systemAdminId;
                ticket.AssignedEmployeeName = displayName;
            }

            if (string.Equals(ticket.TicketStatus, "Open", StringComparison.OrdinalIgnoreCase))
            {
                ticket.TicketStatus = "In_Progress";
            }

            await SaveStateAsync(state, cancellationToken);
            return newMsg;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> UpdateTicketStatusAsync(
        int ticketId,
        int systemAdminId,
        string adminFullName,
        UpdateTicketStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                ticket.TicketStatus = NormalizeStatus(request.Status);
            }

            if (request.Resolution is not null)
            {
                ticket.TicketResolution = string.IsNullOrWhiteSpace(request.Resolution)
                    ? null
                    : request.Resolution.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.AssignedEmployeeName))
            {
                ticket.AssignedEmployeeName = request.AssignedEmployeeName.Trim();
                ticket.AssignedEmployeeId = request.AssignedEmployeeId ?? systemAdminId;
            }
            else if (string.IsNullOrWhiteSpace(ticket.AssignedEmployeeName) && !string.IsNullOrWhiteSpace(adminFullName))
            {
                ticket.AssignedEmployeeId = systemAdminId;
                ticket.AssignedEmployeeName = adminFullName.Trim();
            }

            if (string.Equals(ticket.TicketStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                ticket.TicketResolvedAt = now;
            }

            ticket.UpdatedAt = now;
            await SaveStateAsync(state, cancellationToken);
            return CloneTicket(ticket);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<(IReadOnlyList<AdminNotificationDto> Items, int UnreadCount)> GetAdminNotificationsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var unreadCount = state.AdminNotifications.Count(n => n.ReadAtUtc is null);

            IEnumerable<AdminNotificationDto> query = state.AdminNotifications
                .OrderByDescending(n => n.CreatedAtUtc)
                .ThenByDescending(n => n.NotificationId);

            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc is null);
            }
            else if (string.Equals(status, "read", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc is not null);
            }

            var items = query
                .Take(Math.Clamp(limit, 1, 50))
                .ToList();

            return (items, unreadCount);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> GetAdminUnreadNotificationCountAsync(
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            return state.AdminNotifications.Count(n => n.ReadAtUtc is null);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<bool> MarkAdminNotificationReadAsync(
        int notificationId,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var notif = state.AdminNotifications.FirstOrDefault(n => n.NotificationId == notificationId);
            if (notif is null)
            {
                return false;
            }

            if (notif.ReadAtUtc is null)
            {
                notif.ReadAtUtc = DateTime.UtcNow;
                await SaveStateAsync(state, cancellationToken);
            }

            return true;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> MarkAllAdminNotificationsReadAsync(
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var changed = 0;

            foreach (var notif in state.AdminNotifications.Where(n => n.ReadAtUtc is null))
            {
                notif.ReadAtUtc = now;
                changed++;
            }

            if (changed > 0)
            {
                await SaveStateAsync(state, cancellationToken);
            }

            return changed;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    private async Task SendNewTicketEmailsSafeAsync(
        SupportTicketDto ticket,
        string senderDisplayName,
        string? attachmentSummary)
    {
        try
        {
            var recipients = await ResolveSupportHandlerEmailsAsync();
            if (recipients.Count == 0)
            {
                _logger.LogWarning("Không tìm thấy địa chỉ email người xử lý hỗ trợ để gửi thông báo Ticket {TicketCode}.", ticket.TicketCode);
                return;
            }

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            foreach (var recipient in recipients)
            {
                try
                {
                    var emailMsg = SupportTicketEmailTemplate.BuildNewTicketEmail(
                        recipient,
                        ticket,
                        senderDisplayName,
                        attachmentSummary,
                        systemAdminUrl);

                    await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
                    _logger.LogInformation(
                        "Đã gửi email thông báo Ticket hỗ trợ mới {TicketCode} tới người xử lý hỗ trợ {Recipient}.",
                        ticket.TicketCode,
                        recipient);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Không thể gửi email thông báo Ticket {TicketCode} tới {Recipient}.",
                        ticket.TicketCode,
                        recipient);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xử lý gửi email thông báo Ticket mới {TicketCode}.", ticket.TicketCode);
        }
    }

    private async Task SendCustomerReplyEmailsSafeAsync(
        SupportTicketDto ticket,
        SupportTicketMessageDto message)
    {
        try
        {
            var recipients = await ResolveSupportHandlerEmailsAsync();
            if (recipients.Count == 0)
            {
                return;
            }

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            foreach (var recipient in recipients)
            {
                try
                {
                    var emailMsg = SupportTicketEmailTemplate.BuildCustomerReplyEmail(
                        recipient,
                        ticket,
                        message,
                        systemAdminUrl);

                    await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
                    _logger.LogInformation(
                        "Đã gửi email thông báo phản hồi Ticket {TicketCode} tới người xử lý hỗ trợ {Recipient}.",
                        ticket.TicketCode,
                        recipient);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Không thể gửi email phản hồi Ticket {TicketCode} tới {Recipient}.",
                        ticket.TicketCode,
                        recipient);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi email thông báo phản hồi Ticket {TicketCode}.", ticket.TicketCode);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveSupportHandlerEmailsAsync()
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Lấy email của các SystemAdmin đang hoạt động trong CentralDbContext
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var centralDb = scope.ServiceProvider.GetService<CentralDbContext>();
            if (centralDb is not null)
            {
                var adminEmails = await centralDb.SystemAdmins
                    .AsNoTracking()
                    .Where(a => a.IsActive && a.Email != null && a.Email != "")
                    .Select(a => a.Email!)
                    .ToListAsync();

                foreach (var email in adminEmails)
                {
                    var trimmed = email.Trim();
                    // Bỏ qua email placeholder mặc định "Me@gmail.com" của bootstrap nếu chưa cập nhật
                    if (!string.Equals(trimmed, "Me@gmail.com", StringComparison.OrdinalIgnoreCase)
                        && SmtpEmailOptions.IsEmailAddress(trimmed))
                    {
                        recipients.Add(trimmed);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể truy vấn danh sách email SystemAdmin từ CentralDbContext.");
        }

        // 2. Lấy thêm từ cấu hình SupportTicket:HandlerEmail (nếu có)
        var configuredHandlerEmail = _configuration["SupportTicket:HandlerEmail"];
        if (!string.IsNullOrWhiteSpace(configuredHandlerEmail))
        {
            foreach (var part in configuredHandlerEmail.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (SmtpEmailOptions.IsEmailAddress(part))
                {
                    recipients.Add(part);
                }
            }
        }

        // 3. Fallback về địa chỉ email quản trị cấu hình trong Email:Smtp (FromAddress / Username)
        // để đảm bảo luôn gửi được thông báo tới hòm thư của người xử lý hỗ trợ.
        var smtp = _smtpOptions.Value;
        if (recipients.Count == 0)
        {
            if (SmtpEmailOptions.IsEmailAddress(smtp.FromAddress))
            {
                recipients.Add(smtp.FromAddress!);
            }
            else if (SmtpEmailOptions.IsEmailAddress(smtp.Username))
            {
                recipients.Add(smtp.Username!);
            }
        }

        return recipients.ToList();
    }

    private string BuildSystemAdminTicketUrl(int ticketSupportId)
    {
        var baseUrl = _configuration["SupportTicket:SystemAdminBaseUrl"]
            ?? _configuration["AccountRecovery:SystemAdminFrontendBaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:3001";
        }

        return $"{baseUrl.TrimEnd('/')}/support-tickets/{ticketSupportId}";
    }

    private static IEnumerable<SupportTicketDto> ApplyFilters(
        IEnumerable<SupportTicketDto> query,
        string? status,
        string? priority,
        string? department,
        string? keyword)
    {
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(t => string.Equals(NormalizeStatus(t.TicketStatus), normalizedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(priority) && !string.Equals(priority, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => string.Equals(t.TicketPriority, priority.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(department) && !string.Equals(department, "All", StringComparison.OrdinalIgnoreCase))
        {
            var dept = department.Trim();
            query = query.Where(t => string.Equals(t.Department, dept, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(t =>
                t.TicketCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || t.TicketSubject.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || t.TicketDescription.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || t.TenantName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || t.TenantCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || t.Department.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    /// <summary>
    /// Chuẩn hóa về 3 trạng thái xử lý cốt lõi:
    /// - Open: Chờ tiếp nhận
    /// - In_Progress: Đang xử lý
    /// - Resolved: Đã xử lý
    /// </summary>
    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Open";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "in_progress" or "inprogress" or "pending_customer" or "pendingcustomer" => "In_Progress",
            "resolved" or "closed" or "completed" => "Resolved",
            _ => "Open"
        };
    }

    private static string NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return "Medium";
        }

        return priority.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "urgent" => "Urgent",
            _ => "Medium"
        };
    }

    private async Task<SupportTicketStoreState> EnsureStateLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cachedState is not null)
        {
            return _cachedState;
        }

        if (File.Exists(_storeFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_storeFilePath, cancellationToken);
                var loaded = JsonSerializer.Deserialize<SupportTicketStoreState>(json, JsonOptions);
                if (loaded is not null)
                {
                    foreach (var t in loaded.Tickets)
                    {
                        t.TicketStatus = NormalizeStatus(t.TicketStatus);
                    }
                    _cachedState = loaded;
                    return _cachedState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc file lưu trữ SupportTicket, khởi tạo lại dữ liệu mặc định.");
            }
        }

        _cachedState = CreateSeedState();
        await SaveStateAsync(_cachedState, cancellationToken);
        return _cachedState;
    }

    private async Task SaveStateAsync(SupportTicketStoreState state, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_storeFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi file lưu trữ SupportTicket tại {Path}.", _storeFilePath);
        }
    }

    private static SupportTicketDto CloneTicket(SupportTicketDto src) => new()
    {
        TicketSupportId = src.TicketSupportId,
        CustomerId = src.CustomerId,
        TenantId = src.TenantId,
        TenantName = src.TenantName,
        TenantCode = src.TenantCode,
        CreatedByEmployeeId = src.CreatedByEmployeeId,
        CreatedByName = src.CreatedByName,
        TicketCode = src.TicketCode,
        TicketSubject = src.TicketSubject,
        TicketDescription = src.TicketDescription,
        TicketPriority = src.TicketPriority,
        TicketStatus = src.TicketStatus,
        Department = src.Department,
        Module = src.Module,
        TicketResolution = src.TicketResolution,
        TicketResolvedAt = src.TicketResolvedAt,
        TicketClosedAt = src.TicketClosedAt,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        AssignedEmployeeId = src.AssignedEmployeeId,
        AssignedEmployeeName = src.AssignedEmployeeName,
        Messages = src.Messages.Select(m => new SupportTicketMessageDto
        {
            TicketMessageId = m.TicketMessageId,
            TicketSupportId = m.TicketSupportId,
            SenderCustomerId = m.SenderCustomerId,
            SenderEmployeeId = m.SenderEmployeeId,
            SenderName = m.SenderName,
            IsFromEmployee = m.IsFromEmployee,
            TicketMessageContent = m.TicketMessageContent,
            TicketMessageType = m.TicketMessageType,
            TicketMessageAttachmentUrl = m.TicketMessageAttachmentUrl,
            TicketMessageAttachmentName = m.TicketMessageAttachmentName,
            CreatedAt = m.CreatedAt
        }).ToList()
    };

    private static SupportTicketStoreState CreateSeedState() => new()
    {
        NextTicketId = 1,
        NextMessageId = 100,
        NextNotificationId = 1,
        Tickets = [],
        AdminNotifications = []
    };
}

