using System.Text.Json;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Email;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Support;

public interface ISupportTicketService
{
    Task<IReadOnlyList<SupportTicketDto>> GetAllForAdminAsync(
        int currentEmployeeId,
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
        int currentEmployeeId = 0,
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

    Task<IReadOnlyList<DepartmentEmployeeDto>> GetDepartmentStaffAsync(
        int ticketId,
        int currentEmployeeId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> AssignToStaffAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        AssignTicketToStaffRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> StaffCompleteAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        StaffCompleteTicketRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDto?> HeadResolveAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        HeadResolveTicketRequestDto request,
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

    Task<SupportTicketMessageDto?> AddStaffMessageAsync(
        int ticketId,
        int employeeId,
        string employeeName,
        string content,
        bool isInternalNote = false,
        string? attachmentUrl = null,
        string? attachmentName = null,
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
        int currentEmployeeId,
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> GetAdminUnreadNotificationCountAsync(
        int currentEmployeeId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAdminNotificationReadAsync(
        int currentEmployeeId,
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAdminNotificationsReadAsync(
        int currentEmployeeId,
        CancellationToken cancellationToken = default);

    Task<(string Role, int? DepartmentId, string? DepartmentName)> ResolveEmployeeRoleInfoAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportServiceItemDto>> GetAvailableServicesAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SupportTicketStoreState
{
    public int NextTicketId { get; set; } = 100;
    public int NextMessageId { get; set; } = 1000;
    public int NextNotificationId { get; set; } = 100;
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
    private static bool _tablesEnsured = false;

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

    private static async Task EnsureDatabaseTablesAsync(DbDtctechContext db, CancellationToken ct)
    {
        if (_tablesEnsured) return;
        try
        {
            const string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_TYPES')
BEGIN
    CREATE TABLE [TICKET_TYPES] (
        [ticket_type_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [ticket_name] nvarchar(500) NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_SUPPORT')
BEGIN
    CREATE TABLE [TICKET_SUPPORT] (
        [ticket_support_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [customer_id] bigint NULL,
        [department_id] bigint NULL,
        [tenant_id] bigint NULL,
        [product_id] bigint NULL,
        [service_id] bigint NULL,
        [ticket_code] varchar(30) NULL,
        [ticket_title] nvarchar(1000) NULL,
        [ticket_description] nvarchar(max) NULL,
        [ticket_priority] varchar(20) NULL,
        [ticket_status] varchar(30) NULL,
        [ticket_resolution] nvarchar(max) NULL,
        [ticket_resolved_at] datetime NULL,
        [ticket_closed_at] datetime NULL,
        [created_at] datetime NOT NULL,
        [updated_at] datetime NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_MESSAGE')
BEGIN
    CREATE TABLE [TICKET_MESSAGE] (
        [ticket_message_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [ticket_support_id] bigint NOT NULL,
        [ticket_message_sender_customer_id] bigint NULL,
        [ticket_message_sender_employee_id] bigint NULL,
        [ticket_message_content] nvarchar(max) NULL,
        [ticket_message_type] varchar(20) NULL,
        [created_at] datetime NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_ATTACHMENTS')
BEGIN
    CREATE TABLE [TICKET_ATTACHMENTS] (
        [ticket_attachment_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [ticket_message_id] bigint NULL,
        [file_name_1] varchar(1000) NULL,
        [file_name_2] varchar(1000) NULL,
        [file_name_3] varchar(1000) NULL,
        [file_size] int NULL,
        [mime_type] varchar(20) NULL,
        [uploaded_at] datetime NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_HISTORY')
BEGIN
    CREATE TABLE [TICKET_HISTORY] (
        [ticket_history_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [ticket_support_id] bigint NULL,
        [employee_id_start] int NULL,
        [employee_id_finished] int NULL,
        [changed_by_user_id] int NULL,
        [notes] nvarchar(500) NULL,
        [old_status] varchar(20) NULL,
        [new_status] varchar(20) NULL,
        [changed_at] datetime NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_SUPPORT_EMPLOYEE')
BEGIN
    CREATE TABLE [TICKET_SUPPORT_EMPLOYEE] (
        [ticket_support_employee_id] bigint PRIMARY KEY IDENTITY(1, 1),
        [ticket_support_id] bigint NOT NULL,
        [employee_id] bigint NOT NULL,
        [ticket_support_employee_assignment_role] varchar(20) NULL,
        [ticket_support_employee_assigned_at] datetime NULL,
        [ticket_support_employee_unassigned_at] datetime NULL,
        [ticket_support_employee_assigned_by_employee_id] bigint NULL
    );
END;
";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            _tablesEnsured = true;
        }
        catch
        {
            // Fallback nếu provider không hỗ trợ raw SQL DDL
        }
    }

    public async Task<IReadOnlyList<SupportTicketDto>> GetAllForAdminAsync(
        int currentEmployeeId,
        string? status,
        string? priority,
        string? department,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);

            // System Admin không nhận và không xem yêu cầu hỗ trợ khách hàng
            if (string.Equals(roleInfo.Role, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var dbTickets = await LoadTicketsFromDbAsync(cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);

            var combined = MergeTickets(dbTickets, state.Tickets);
            IEnumerable<SupportTicketDto> query = combined
                .Where(t => CanEmployeeViewTicket(t, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId))
                .OrderByDescending(t => t.CreatedAt);

            query = ApplyFilters(query, status, priority, department, keyword);
            return query.Select(t => CloneTicketWithViewer(t, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId)).ToList();
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
            var dbTickets = await LoadTicketsFromDbAsync(cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var combined = MergeTickets(dbTickets, state.Tickets);

            IEnumerable<SupportTicketDto> query = combined
                .Where(t => BelongsToCustomerTenant(t, tenantId, tenantCode, employeeId))
                .OrderByDescending(t => t.CreatedAt);

            query = ApplyFilters(query, status, priority, null, keyword);
            return query.Select(t => CloneTicketWithViewer(t, "CUSTOMER", employeeId, null)).ToList();
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> GetByIdAsync(
        int ticketId,
        int currentEmployeeId = 0,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (string.Equals(roleInfo.Role, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var dbTicket = await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var stateTicket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);

            SupportTicketDto? ticket = null;
            if (dbTicket != null && stateTicket != null)
            {
                ticket = MergeTickets([dbTicket], [stateTicket]).FirstOrDefault();
            }
            else
            {
                ticket = stateTicket ?? dbTicket;
            }

            if (ticket is null) return null;
            if (!CanEmployeeViewTicket(ticket, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }

            return CloneTicketWithViewer(ticket, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId);
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
            var dbTicket = await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var stateTicket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);

            SupportTicketDto? ticket = null;
            if (dbTicket != null && stateTicket != null)
            {
                ticket = MergeTickets([dbTicket], [stateTicket]).FirstOrDefault();
            }
            else
            {
                ticket = stateTicket ?? dbTicket;
            }

            if (ticket is null) return null;

            if (!BelongsToCustomerTenant(ticket, tenantId, tenantCode, employeeId))
            {
                return null;
            }

            return CloneTicketWithViewer(ticket, "CUSTOMER", employeeId, null);
        }
        finally
        {
            StoreLock.Release();
        }
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
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);

            var resolvedDeptId = ResolveDepartmentIdFromInput(request.DepartmentId, request.Department);
            var departmentDisplayName = GetDepartmentDisplayName(resolvedDeptId, request.Department);
            var resolvedServiceId = request.ServiceId;
            var resolvedServiceName = !string.IsNullOrWhiteSpace(request.ServiceName)
                ? request.ServiceName.Trim()
                : request.Module?.Trim();
            TblDepartment? dept = null;
            TblEmployee? head = null;
            var ticketCode = $"TK-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            int? persistedTicketId = null;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    await EnsureDatabaseTablesAsync(db, cancellationToken);

                    if (resolvedServiceId.HasValue && resolvedServiceId.Value > 0 && string.IsNullOrWhiteSpace(resolvedServiceName))
                    {
                        var svc = await db.SERVICES.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.ServiceId == (int)resolvedServiceId.Value, cancellationToken);
                        if (svc != null && !string.IsNullOrWhiteSpace(svc.ServiceName))
                        {
                            resolvedServiceName = svc.ServiceName;
                        }
                    }
                    else if (!resolvedServiceId.HasValue && !string.IsNullOrWhiteSpace(resolvedServiceName))
                    {
                        var svc = await db.SERVICES.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.ServiceName == resolvedServiceName, cancellationToken);
                        if (svc != null)
                        {
                            resolvedServiceId = svc.ServiceId;
                        }
                    }

                    dept = await db.TblDepartments.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.DepartmentId == resolvedDeptId, cancellationToken);
                    if (dept != null && !string.IsNullOrWhiteSpace(dept.DepartmentName))
                    {
                        departmentDisplayName = dept.DepartmentName;
                    }

                    // NGHIỆP VỤ: Tìm đúng TRƯỞNG PHÒNG của phòng ban mà khách hàng gửi yêu cầu (KHÔNG gửi cho sysadmin hay phòng ban khác)
                    head = await FindDepartmentHeadAsync(db, resolvedDeptId, cancellationToken);

                    try
                    {
                        var entity = new TICKET_SUPPORT
                        {
                            customer_id = employeeId > 0 ? employeeId : 1,
                            tenant_id = tenantId > 0 ? tenantId : 1,
                            department_id = resolvedDeptId,
                            product_id = request.ProductId,
                            service_id = resolvedServiceId,
                            ticket_code = ticketCode,
                            ticket_title = request.ResolvedSubject,
                            ticket_description = request.ResolvedDescription,
                            ticket_priority = NormalizePriority(request.ResolvedPriority),
                            ticket_status = "Open",
                            created_at = DateTime.UtcNow
                        };

                        db.TICKET_SUPPORT.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);
                        persistedTicketId = (int)entity.ticket_support_id;

                        // Lưu vào TICKET_SUPPORT_EMPLOYEE với role = DEPARTMENT_HEAD (chỉ Trưởng phòng của phòng ban này)
                        if (head != null)
                        {
                            var headAssignment = new TICKET_SUPPORT_EMPLOYEE
                            {
                                ticket_support_id = entity.ticket_support_id,
                                employee_id = head.EmployeeId,
                                ticket_support_employee_assignment_role = "DEPARTMENT_HEAD",
                                ticket_support_employee_assigned_at = DateTime.UtcNow,
                                ticket_support_employee_assigned_by_employee_id = null
                            };
                            db.TICKET_SUPPORT_EMPLOYEE.Add(headAssignment);
                        }

                        var initMsg = new TICKET_MESSAGE
                        {
                            ticket_support_id = entity.ticket_support_id,
                            ticket_message_sender_customer_id = employeeId > 0 ? employeeId : 1,
                            ticket_message_content = request.ResolvedDescription,
                            ticket_message_type = "INITIAL",
                            created_at = DateTime.UtcNow
                        };
                        db.TICKET_MESSAGE.Add(initMsg);
                        await db.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(request.AttachmentUrls?.FirstOrDefault())
                            || !string.IsNullOrWhiteSpace(request.AttachmentNames?.FirstOrDefault()))
                        {
                            var rawUrl = request.AttachmentUrls?.FirstOrDefault() ?? request.AttachmentNames?.FirstOrDefault();
                            var safeDbUrl = rawUrl != null && rawUrl.Length > 950
                                ? (request.AttachmentNames?.FirstOrDefault() ?? "image-attachment.png")
                                : rawUrl;
                            var safeDbName = request.AttachmentNames?.FirstOrDefault();
                            if (safeDbName != null && safeDbName.Length > 950)
                            {
                                safeDbName = safeDbName[..950];
                            }

                            var initAtt = new TICKET_ATTACHMENTS
                            {
                                ticket_message_id = initMsg.ticket_message_id,
                                file_name_1 = safeDbUrl,
                                file_name_2 = safeDbName,
                                mime_type = "image/png",
                                uploaded_at = DateTime.UtcNow
                            };
                            db.TICKET_ATTACHMENTS.Add(initAtt);
                        }

                        var initHistory = new TICKET_HISTORY
                        {
                            ticket_support_id = entity.ticket_support_id,
                            employee_id_start = head != null ? (int)head.EmployeeId : null,
                            old_status = null,
                            new_status = "Open",
                            notes = $"Khởi tạo phiếu hỗ trợ bởi {senderName} gửi tới {departmentDisplayName}.",
                            changed_at = DateTime.UtcNow
                        };
                        db.TICKET_HISTORY.Add(initHistory);

                        await db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Đã lưu Ticket {TicketCode} cho phòng ban #{DeptId} ({DeptName}), Trưởng phòng nhận: {HeadName}.",
                            ticketCode, resolvedDeptId, departmentDisplayName, head?.EmployeeFullName ?? "Chưa xác định");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lưu vào database thất bại, sử dụng backup store.");
                    }
                }
            }

            var ticketId = persistedTicketId ?? state.NextTicketId++;
            if (state.NextTicketId <= ticketId)
            {
                state.NextTicketId = ticketId + 1;
            }

            var ticket = new SupportTicketDto
            {
                TicketSupportId = ticketId,
                CustomerId = employeeId > 0 ? employeeId : 1,
                TenantId = tenantId,
                TenantName = tenantName,
                TenantCode = tenantCode,
                CreatedByEmployeeId = employeeId,
                CreatedByName = senderName,
                TicketCode = ticketCode,
                TicketSubject = request.ResolvedSubject,
                TicketDescription = request.ResolvedDescription,
                TicketPriority = NormalizePriority(request.ResolvedPriority),
                TicketStatus = "Open",
                DepartmentId = resolvedDeptId,
                Department = departmentDisplayName,
                ServiceId = resolvedServiceId,
                ServiceName = resolvedServiceName,
                Module = resolvedServiceName ?? request.Module ?? "Dịch vụ chung",
                CreatedAt = DateTime.UtcNow,
                DepartmentHeadEmployeeId = head?.EmployeeId,
                DepartmentHeadName = head?.EmployeeFullName ?? head?.EmployeeAccount,
                ViewerRole = "CUSTOMER",
                AttachmentUrls = request.AttachmentUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [],
                AttachmentNames = request.AttachmentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [],
                Assignments = head != null ? [
                    new TicketEmployeeAssignmentDto
                    {
                        TicketSupportEmployeeId = 1,
                        TicketSupportId = ticketId,
                        EmployeeId = head.EmployeeId,
                        EmployeeName = head.EmployeeFullName ?? head.EmployeeAccount ?? "Trưởng phòng",
                        EmployeeEmail = head.EmployeeEmail,
                        AssignmentRole = "DEPARTMENT_HEAD",
                        AssignedAt = DateTime.UtcNow
                    }
                ] : [],
                Messages = [
                    new SupportTicketMessageDto
                    {
                        TicketMessageId = state.NextMessageId++,
                        TicketSupportId = ticketId,
                        SenderCustomerId = employeeId > 0 ? employeeId : 1,
                        SenderName = senderName,
                        IsFromEmployee = false,
                        TicketMessageContent = request.ResolvedDescription,
                        TicketMessageType = "INITIAL",
                        TicketMessageAttachmentName = request.AttachmentNames?.FirstOrDefault(),
                        TicketMessageAttachmentUrl = request.AttachmentUrls?.FirstOrDefault(),
                        AttachmentUrls = request.AttachmentUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [],
                        AttachmentNames = request.AttachmentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [],
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            };

            state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId || string.Equals(t.TicketCode, ticketCode, StringComparison.OrdinalIgnoreCase));
            state.Tickets.Insert(0, ticket);

            // Chỉ tạo Notification cho đúng Trưởng phòng của phòng ban nhận yêu cầu (TargetEmployeeId = head.EmployeeId, DepartmentId = resolvedDeptId)
            var notification = new AdminNotificationDto
            {
                NotificationId = state.NextNotificationId++,
                Title = $"Yêu cầu hỗ trợ mới cần phân công: [{ticket.TicketCode}]",
                Body = $"Khách hàng {senderName} ({tenantName}) gửi yêu cầu tới {ticket.Department}: {ticket.TicketSubject}",
                SubjectType = "SupportTicket",
                SubjectId = ticket.TicketSupportId,
                TicketCode = ticket.TicketCode,
                TenantName = tenantName,
                TenantCode = tenantCode,
                Priority = ticket.TicketPriority,
                DepartmentId = resolvedDeptId,
                Department = ticket.Department,
                TargetEmployeeId = head?.EmployeeId,
                ActionKey = "new-ticket",
                CreatedAtUtc = DateTime.UtcNow
            };
            state.AdminNotifications.Insert(0, notification);

            await SaveStateAsync(state, cancellationToken);

            // Gửi email thông báo CHỈ cho Trưởng phòng của phòng ban đó
            _ = SendNewTicketNotificationAsync(ticket, senderName, head);

            return CloneTicketWithViewer(ticket, "CUSTOMER", employeeId, null);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<IReadOnlyList<DepartmentEmployeeDto>> GetDepartmentStaffAsync(
        int ticketId,
        int currentEmployeeId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<DbDtctechContext>();
        if (db == null) return [];

        int? deptId = null;
        var ticket = await db.TICKET_SUPPORT.AsNoTracking().FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
        if (ticket?.department_id != null)
        {
            deptId = (int)ticket.department_id.Value;
        }
        else
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var memTicket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (memTicket != null)
            {
                deptId = ResolveDepartmentIdFromInput(memTicket.DepartmentId, memTicket.Department);
            }
        }

        if (!deptId.HasValue)
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            deptId = roleInfo.DepartmentId ?? 2;
        }

        var dept = await db.TblDepartments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == deptId.Value, cancellationToken);
        var deptName = dept?.DepartmentName ?? GetDepartmentDisplayName(deptId.Value, null);

        var allActiveEmployees = await db.TblEmployees.AsNoTracking()
            .Where(e => e.Status == 1)
            .OrderBy(e => e.EmployeeFullName)
            .ToListAsync(cancellationToken);

        var empDeptMap = await LoadEmployeeDepartmentMapAsync(db, cancellationToken);

        // Trả về cả Trưởng phòng (isManager = true) và các Nhân viên (STAFF) thuộc đúng phòng ban đó để Trưởng phòng có thể tự nhận xử lý hoặc giao cho nhân viên
        var filteredStaff = new List<DepartmentEmployeeDto>();
        foreach (var emp in allActiveEmployees)
        {
            var (role, empDeptId) = ClassifyEmployee(emp, empDeptMap);
            var isHead = string.Equals(role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase);
            var isStaff = string.Equals(role, "STAFF", StringComparison.OrdinalIgnoreCase);
            if (!isHead && !isStaff)
            {
                continue;
            }
            if (empDeptId != deptId.Value)
            {
                continue;
            }

            filteredStaff.Add(new DepartmentEmployeeDto
            {
                EmployeeId = emp.EmployeeId,
                EmployeeName = !string.IsNullOrWhiteSpace(emp.EmployeeFullName) ? emp.EmployeeFullName : (emp.EmployeeAccount ?? $"NV #{emp.EmployeeId}"),
                EmployeeCode = emp.EmployeeCode,
                EmployeeEmail = emp.EmployeeEmail,
                DepartmentId = deptId.Value,
                DepartmentName = deptName,
                IsManager = isHead
            });
        }

        return filteredStaff
            .OrderByDescending(x => x.IsManager)
            .ThenBy(x => x.EmployeeName)
            .ToList();
    }

    public async Task<SupportTicketDto?> AssignToStaffAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        AssignTicketToStaffRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();

            var assignedPriority = !string.IsNullOrWhiteSpace(request.Priority)
                ? NormalizePriority(request.Priority)
                : null;
            var priorityLabel = (assignedPriority ?? "Medium") switch
            {
                "Urgent" => "Khẩn cấp",
                "High" => "Cao",
                "Low" => "Thấp",
                _ => "Bình thường"
            };

            var targetStaffId = request.StaffEmployeeId > 0 ? request.StaffEmployeeId : currentEmployeeId;
            var isSelfAssign = targetStaffId == currentEmployeeId;

            TblEmployee? staff = null;
            if (db != null)
            {
                staff = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == targetStaffId, cancellationToken);
                var ticketEntity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);

                if (ticketEntity != null && staff != null)
                {
                    ticketEntity.ticket_status = "In_Progress";
                    if (!string.IsNullOrWhiteSpace(assignedPriority))
                    {
                        ticketEntity.ticket_priority = assignedPriority;
                    }
                    ticketEntity.updated_at = DateTime.UtcNow;

                    var assignment = new TICKET_SUPPORT_EMPLOYEE
                    {
                        ticket_support_id = ticketId,
                        employee_id = staff.EmployeeId,
                        ticket_support_employee_assignment_role = "ASSIGNEE",
                        ticket_support_employee_assigned_at = DateTime.UtcNow,
                        ticket_support_employee_assigned_by_employee_id = currentEmployeeId
                    };
                    db.TICKET_SUPPORT_EMPLOYEE.Add(assignment);

                    var noteText = string.IsNullOrWhiteSpace(request.Note)
                        ? (isSelfAssign ? "Trưởng phòng trực tiếp tiếp nhận và xử lý yêu cầu." : "Vui lòng tiếp nhận và xử lý yêu cầu của khách hàng.")
                        : request.Note.Trim();
                    var assignHistory = new TICKET_HISTORY
                    {
                        ticket_support_id = ticketId,
                        employee_id_start = currentEmployeeId,
                        employee_id_finished = (int)staff.EmployeeId,
                        changed_by_user_id = currentEmployeeId,
                        old_status = "Open",
                        new_status = "In_Progress",
                        notes = isSelfAssign
                            ? $"Trưởng phòng {currentEmployeeName} trực tiếp tiếp nhận xử lý yêu cầu (Độ ưu tiên: {priorityLabel}). Ghi chú: {noteText}"
                            : $"Trưởng phòng {currentEmployeeName} phân công cho nhân viên {staff.EmployeeFullName} (Độ ưu tiên: {priorityLabel}). Nội dung chỉ đạo: {noteText}",
                        changed_at = DateTime.UtcNow
                    };
                    db.TICKET_HISTORY.Add(assignHistory);

                    var msg = new TICKET_MESSAGE
                    {
                        ticket_support_id = ticketId,
                        ticket_message_sender_employee_id = currentEmployeeId,
                        ticket_message_type = "ASSIGNMENT",
                        ticket_message_content = isSelfAssign
                            ? $"[Trưởng phòng tự xử lý • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã trực tiếp tiếp nhận xử lý yêu cầu.\nGhi chú: {noteText}"
                            : $"[Phân công xử lý • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã giao yêu cầu cho nhân viên {staff.EmployeeFullName}.\nNội dung chỉ đạo: {noteText}",
                        created_at = DateTime.UtcNow
                    };
                    db.TICKET_MESSAGE.Add(msg);

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);

            if (ticket != null)
            {
                var staffName = staff?.EmployeeFullName ?? (isSelfAssign ? currentEmployeeName : $"Nhân viên #{targetStaffId}");
                var noteText = string.IsNullOrWhiteSpace(request.Note)
                    ? (isSelfAssign ? "Trưởng phòng trực tiếp tiếp nhận và xử lý yêu cầu." : "Vui lòng tiếp nhận và xử lý yêu cầu của khách hàng.")
                    : request.Note.Trim();

                ticket.TicketStatus = "In_Progress";
                if (!string.IsNullOrWhiteSpace(assignedPriority))
                {
                    ticket.TicketPriority = assignedPriority;
                }
                ticket.AssignedEmployeeId = targetStaffId;
                ticket.AssignedEmployeeName = staffName;
                ticket.AssignedAt = DateTime.UtcNow;
                ticket.UpdatedAt = DateTime.UtcNow;

                ticket.Assignments.RemoveAll(a => !string.Equals(a.AssignmentRole, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase));
                ticket.Assignments.Add(new TicketEmployeeAssignmentDto
                {
                    TicketSupportEmployeeId = ticket.Assignments.Count + 1,
                    TicketSupportId = ticketId,
                    EmployeeId = targetStaffId,
                    EmployeeName = staffName,
                    EmployeeEmail = staff?.EmployeeEmail,
                    AssignmentRole = "ASSIGNEE",
                    AssignedAt = DateTime.UtcNow,
                    AssignedByEmployeeId = currentEmployeeId,
                    AssignedByName = currentEmployeeName
                });

                ticket.Messages.Add(new SupportTicketMessageDto
                {
                    TicketMessageId = state.NextMessageId++,
                    TicketSupportId = ticketId,
                    SenderEmployeeId = currentEmployeeId,
                    SenderName = currentEmployeeName,
                    IsFromEmployee = true,
                    TicketMessageContent = isSelfAssign
                        ? $"[Trưởng phòng tự xử lý • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã trực tiếp tiếp nhận xử lý yêu cầu.\nGhi chú: {noteText}"
                        : $"[Phân công xử lý • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã giao yêu cầu cho nhân viên {staffName}.\nNội dung chỉ đạo: {noteText}",
                    TicketMessageType = "ASSIGNMENT",
                    CreatedAt = DateTime.UtcNow
                });

                state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
                state.Tickets.Insert(0, ticket);

                if (!isSelfAssign)
                {
                    // Tạo thông báo riêng cho NHÂN VIÊN ĐƯỢC PHÂN CÔNG
                    state.AdminNotifications.Insert(0, new AdminNotificationDto
                    {
                        NotificationId = state.NextNotificationId++,
                        Title = $"Bạn được phân công xử lý Ticket [{ticket.TicketCode}]",
                        Body = $"Trưởng phòng {currentEmployeeName} đã giao việc cho bạn: {ticket.TicketSubject}",
                        SubjectType = "SupportTicket",
                        SubjectId = ticket.TicketSupportId,
                        TicketCode = ticket.TicketCode,
                        TenantName = ticket.TenantName,
                        TenantCode = ticket.TenantCode,
                        Priority = ticket.TicketPriority,
                        DepartmentId = ticket.DepartmentId,
                        Department = ticket.Department,
                        TargetEmployeeId = targetStaffId,
                        ActionKey = "assigned-ticket",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                await SaveStateAsync(state, cancellationToken);
            }

            if (!isSelfAssign && staff != null && ticket != null)
            {
                _ = SendStaffAssignmentNotificationAsync(ticket, staff, currentEmployeeName, request.Note);
            }

            return ticket is null ? null : CloneTicketWithViewer(ticket, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> StaffCompleteAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        StaffCompleteTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN") return null;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();

            if (db != null)
            {
                var ticketEntity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
                if (ticketEntity != null)
                {
                    ticketEntity.ticket_resolution = request.Resolution;
                    ticketEntity.ticket_status = "PendingHeadApproval";
                    ticketEntity.updated_at = DateTime.UtcNow;

                    var msg = new TICKET_MESSAGE
                    {
                        ticket_support_id = ticketId,
                        ticket_message_sender_employee_id = currentEmployeeId,
                        ticket_message_type = "STAFF_COMPLETION",
                        ticket_message_content = $"[Báo cáo Trưởng phòng] Nhân viên {currentEmployeeName} đã xử lý xong và gửi kết quả lên Trưởng phòng:\n{request.Resolution}",
                        created_at = DateTime.UtcNow
                    };
                    db.TICKET_MESSAGE.Add(msg);

                    var staffHistory = new TICKET_HISTORY
                    {
                        ticket_support_id = ticketId,
                        employee_id_start = currentEmployeeId,
                        changed_by_user_id = currentEmployeeId,
                        old_status = "In_Progress",
                        new_status = "PendingHeadApproval",
                        notes = $"Nhân viên {currentEmployeeName} gửi kết quả lên Trưởng phòng: {request.Resolution}",
                        changed_at = DateTime.UtcNow
                    };
                    db.TICKET_HISTORY.Add(staffHistory);

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket != null)
            {
                if (!CanEmployeeViewTicket(ticket, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId))
                {
                    return null;
                }

                ticket.TicketResolution = request.Resolution;
                ticket.TicketStatus = "PendingHeadApproval";
                ticket.UpdatedAt = DateTime.UtcNow;

                ticket.Messages.Add(new SupportTicketMessageDto
                {
                    TicketMessageId = state.NextMessageId++,
                    TicketSupportId = ticketId,
                    SenderEmployeeId = currentEmployeeId,
                    SenderName = currentEmployeeName,
                    IsFromEmployee = true,
                    TicketMessageContent = $"[Báo cáo Trưởng phòng] Nhân viên {currentEmployeeName} đã xử lý xong và gửi kết quả lên Trưởng phòng:\n{request.Resolution}",
                    TicketMessageType = "STAFF_COMPLETION",
                    CreatedAt = DateTime.UtcNow
                });

                state.AdminNotifications.Insert(0, new AdminNotificationDto
                {
                    NotificationId = state.NextNotificationId++,
                    SubjectType = "SupportTicket",
                    SubjectId = ticket.TicketSupportId,
                    TicketCode = ticket.TicketCode,
                    TenantName = ticket.TenantName,
                    TenantCode = ticket.TenantCode,
                    Title = $"[{ticket.Department}] NV {currentEmployeeName} báo cáo hoàn tất {ticket.TicketCode}",
                    Body = $"Nhân viên {currentEmployeeName} đã gửi kết quả xử lý ticket '{ticket.TicketSubject}' lên Trưởng phòng.",
                    Priority = ticket.TicketPriority,
                    DepartmentId = ticket.DepartmentId,
                    Department = ticket.Department,
                    TargetEmployeeId = ticket.DepartmentHeadEmployeeId,
                    ActionKey = "staff-completed-ticket",
                    CreatedAtUtc = DateTime.UtcNow
                });

                await SaveStateAsync(state, cancellationToken);
            }

            return ticket is null ? null : CloneTicketWithViewer(ticket, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketDto?> HeadResolveAsync(
        int ticketId,
        int currentEmployeeId,
        string currentEmployeeName,
        HeadResolveTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN") return null;

            var roleLabel = roleInfo.Role == "DEPARTMENT_HEAD" ? "Trưởng phòng" : "Nhân viên phụ trách";
            var targetStatus = request.CloseTicket ? "Resolved" : "In_Progress";
            var resolutionText = request.Resolution;
            var customerNotice = !string.IsNullOrWhiteSpace(request.MessageToCustomer)
                ? request.MessageToCustomer
                : $"[Kết quả xử lý từ {roleLabel} {currentEmployeeName}]:\n{resolutionText}";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();

            if (db != null)
            {
                var ticketEntity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
                if (ticketEntity != null)
                {
                    var oldStatus = ticketEntity.ticket_status ?? "In_Progress";
                    ticketEntity.ticket_resolution = resolutionText;
                    ticketEntity.ticket_status = targetStatus;
                    ticketEntity.ticket_resolved_at = DateTime.UtcNow;
                    if (request.CloseTicket) ticketEntity.ticket_closed_at = DateTime.UtcNow;
                    ticketEntity.updated_at = DateTime.UtcNow;

                    var msg = new TICKET_MESSAGE
                    {
                        ticket_support_id = ticketId,
                        ticket_message_sender_employee_id = currentEmployeeId,
                        ticket_message_type = "RESOLUTION",
                        ticket_message_content = customerNotice,
                        created_at = DateTime.UtcNow
                    };
                    db.TICKET_MESSAGE.Add(msg);

                    var headHistory = new TICKET_HISTORY
                    {
                        ticket_support_id = ticketId,
                        employee_id_start = currentEmployeeId,
                        changed_by_user_id = currentEmployeeId,
                        old_status = oldStatus,
                        new_status = targetStatus,
                        notes = $"{roleLabel} {currentEmployeeName} gửi kết quả cho khách hàng: {customerNotice}",
                        changed_at = DateTime.UtcNow
                    };
                    db.TICKET_HISTORY.Add(headHistory);

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket != null)
            {
                if (!CanEmployeeViewTicket(ticket, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId))
                {
                    return null;
                }

                // Nếu Trưởng phòng trực tiếp xử lý luôn mà chưa phân công nhân viên, gán Trưởng phòng là người xử lý
                if (!ticket.AssignedEmployeeId.HasValue || ticket.AssignedEmployeeId <= 0)
                {
                    ticket.AssignedEmployeeId = currentEmployeeId;
                    ticket.AssignedEmployeeName = currentEmployeeName;
                    ticket.AssignedAt = DateTime.UtcNow;
                    if (!ticket.Assignments.Any(a => a.EmployeeId == currentEmployeeId && a.AssignmentRole == "ASSIGNEE"))
                    {
                        ticket.Assignments.Add(new TicketEmployeeAssignmentDto
                        {
                            TicketSupportEmployeeId = ticket.Assignments.Count + 1,
                            TicketSupportId = ticketId,
                            EmployeeId = currentEmployeeId,
                            EmployeeName = currentEmployeeName,
                            AssignmentRole = "ASSIGNEE",
                            AssignedAt = DateTime.UtcNow,
                            AssignedByEmployeeId = currentEmployeeId,
                            AssignedByName = currentEmployeeName
                        });
                    }
                }

                ticket.TicketResolution = resolutionText;
                ticket.TicketStatus = targetStatus;
                ticket.TicketResolvedAt = DateTime.UtcNow;
                if (request.CloseTicket) ticket.TicketClosedAt = DateTime.UtcNow;
                ticket.UpdatedAt = DateTime.UtcNow;

                ticket.Messages.Add(new SupportTicketMessageDto
                {
                    TicketMessageId = state.NextMessageId++,
                    TicketSupportId = ticketId,
                    SenderEmployeeId = currentEmployeeId,
                    SenderName = currentEmployeeName,
                    IsFromEmployee = true,
                    TicketMessageContent = customerNotice,
                    TicketMessageType = "RESOLUTION",
                    CreatedAt = DateTime.UtcNow
                });

                state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
                state.Tickets.Insert(0, ticket);
                await SaveStateAsync(state, cancellationToken);

                // Gửi email thông báo kết quả giải quyết cho khách hàng
                _ = SendResolutionNoticeToCustomerAsync(ticket, customerNotice);
            }

            return ticket is null ? null : CloneTicketWithViewer(ticket, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId);
        }
        finally
        {
            StoreLock.Release();
        }
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
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var rawContent = (request.MessageContent ?? request.Content ?? string.Empty).Trim();
            var attUrls = request.AttachmentUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
            if (!string.IsNullOrWhiteSpace(request.AttachmentUrl) && !attUrls.Contains(request.AttachmentUrl))
            {
                attUrls.Insert(0, request.AttachmentUrl);
            }
            var attNames = request.AttachmentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
            if (!string.IsNullOrWhiteSpace(request.AttachmentName) && !attNames.Contains(request.AttachmentName))
            {
                attNames.Insert(0, request.AttachmentName);
            }

            var firstUrl = attUrls.FirstOrDefault();
            var firstName = attNames.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(rawContent) && string.IsNullOrWhiteSpace(firstUrl))
            {
                return null;
            }
            var content = string.IsNullOrWhiteSpace(rawContent) ? "[Hình ảnh đính kèm]" : rawContent;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    try
                    {
                        var entity = new TICKET_MESSAGE
                        {
                            ticket_support_id = ticketId,
                            ticket_message_sender_customer_id = employeeId > 0 ? employeeId : 1,
                            ticket_message_content = content,
                            ticket_message_type = "CUSTOMER_REPLY",
                            created_at = DateTime.UtcNow
                        };
                        db.TICKET_MESSAGE.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(firstUrl) || !string.IsNullOrWhiteSpace(firstName))
                        {
                            var safeDbUrl = firstUrl != null && firstUrl.Length > 950
                                ? (firstName ?? "image-attachment.png")
                                : (firstUrl ?? firstName);
                            var safeDbName = firstName != null && firstName.Length > 950
                                ? firstName[..950]
                                : firstName;

                            var att = new TICKET_ATTACHMENTS
                            {
                                ticket_message_id = entity.ticket_message_id,
                                file_name_1 = safeDbUrl,
                                file_name_2 = safeDbName,
                                mime_type = "image/png",
                                uploaded_at = DateTime.UtcNow
                            };
                            db.TICKET_ATTACHMENTS.Add(att);
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi ghi DB nếu chuỗi đính kèm vượt giới hạn cột
                    }
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket is null) return null;

            var msg = new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderCustomerId = employeeId,
                SenderName = senderName,
                IsFromEmployee = false,
                TicketMessageContent = content,
                TicketMessageType = "CUSTOMER_REPLY",
                TicketMessageAttachmentName = firstName,
                TicketMessageAttachmentUrl = firstUrl,
                AttachmentUrls = attUrls,
                AttachmentNames = attNames,
                CreatedAt = DateTime.UtcNow
            };

            ticket.Messages.Add(msg);
            ticket.UpdatedAt = DateTime.UtcNow;
            state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
            state.Tickets.Insert(0, ticket);

            // Tạo thông báo cho nhân viên được phân công (nếu đã phân công) hoặc Trưởng phòng của phòng ban đó
            var targetEmpId = ticket.AssignedEmployeeId ?? ticket.DepartmentHeadEmployeeId;
            state.AdminNotifications.Insert(0, new AdminNotificationDto
            {
                NotificationId = state.NextNotificationId++,
                SubjectType = "SupportTicket",
                SubjectId = ticket.TicketSupportId,
                TicketCode = ticket.TicketCode,
                TenantName = ticket.TenantName,
                TenantCode = ticket.TenantCode,
                Title = $"[{ticket.Department}] Khách hàng phản hồi {ticket.TicketCode}",
                Body = $"{senderName}: {content}",
                Priority = ticket.TicketPriority,
                DepartmentId = ticket.DepartmentId,
                Department = ticket.Department,
                TargetEmployeeId = targetEmpId,
                ActionKey = "customer-replied-ticket",
                CreatedAtUtc = DateTime.UtcNow
            });

            await SaveStateAsync(state, cancellationToken);
            _ = SendCustomerReplyEmailsSafeAsync(ticket, msg);

            return msg;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketMessageDto?> AddStaffMessageAsync(
        int ticketId,
        int employeeId,
        string employeeName,
        string content,
        bool isInternalNote = false,
        string? attachmentUrl = null,
        string? attachmentName = null,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var normalizedContent = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedContent) && string.IsNullOrWhiteSpace(attachmentUrl))
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(normalizedContent))
            {
                normalizedContent = "[Hình ảnh đính kèm]";
            }

            var roleInfo = await ResolveEmployeeRoleInfoAsync(employeeId, cancellationToken);
            // Chỉ Trưởng phòng (DEPARTMENT_HEAD) mới được nhắn tin trao đổi trực tiếp với khách hàng; Nhân viên được phân công (STAFF) và SYSADMIN không nhắn tin.
            if (!string.Equals(roleInfo.Role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket is null) return null;

            if (!CanEmployeeViewTicket(ticket, employeeId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    try
                    {
                        var entity = new TICKET_MESSAGE
                        {
                            ticket_support_id = ticketId,
                            ticket_message_sender_employee_id = employeeId,
                            ticket_message_content = normalizedContent,
                            ticket_message_type = isInternalNote ? "INTERNAL_NOTE" : "STAFF_REPLY",
                            created_at = DateTime.UtcNow
                        };
                        db.TICKET_MESSAGE.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(attachmentUrl) || !string.IsNullOrWhiteSpace(attachmentName))
                        {
                            var safeDbUrl = attachmentUrl != null && attachmentUrl.Length > 950
                                ? (attachmentName ?? "image-attachment.png")
                                : (attachmentUrl ?? attachmentName);
                            var safeDbName = attachmentName != null && attachmentName.Length > 950
                                ? attachmentName[..950]
                                : attachmentName;

                            var att = new TICKET_ATTACHMENTS
                            {
                                ticket_message_id = entity.ticket_message_id,
                                file_name_1 = safeDbUrl,
                                file_name_2 = safeDbName,
                                mime_type = "image/png",
                                uploaded_at = DateTime.UtcNow
                            };
                            db.TICKET_ATTACHMENTS.Add(att);
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi ghi DB nếu chuỗi đính kèm vượt giới hạn cột
                    }
                }
            }

            var msg = new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderEmployeeId = employeeId,
                SenderName = employeeName,
                IsFromEmployee = true,
                TicketMessageContent = normalizedContent,
                TicketMessageType = isInternalNote ? "INTERNAL_NOTE" : "STAFF_REPLY",
                TicketMessageAttachmentName = attachmentName,
                TicketMessageAttachmentUrl = attachmentUrl,
                AttachmentUrls = !string.IsNullOrWhiteSpace(attachmentUrl) ? [attachmentUrl] : [],
                AttachmentNames = !string.IsNullOrWhiteSpace(attachmentName) ? [attachmentName] : [],
                CreatedAt = DateTime.UtcNow
            };

            ticket.Messages.Add(msg);
            ticket.UpdatedAt = DateTime.UtcNow;
            state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
            state.Tickets.Insert(0, ticket);

            await SaveStateAsync(state, cancellationToken);
            return msg;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<SupportTicketMessageDto?> AddAdminMessageAsync(
        int ticketId,
        int systemAdminId,
        string adminFullName,
        AdminReplyTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var attUrl = !string.IsNullOrWhiteSpace(request.AttachmentUrl)
            ? request.AttachmentUrl
            : request.AttachmentUrls?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        var attName = !string.IsNullOrWhiteSpace(request.AttachmentName)
            ? request.AttachmentName
            : request.AttachmentNames?.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        return await AddStaffMessageAsync(
            ticketId,
            systemAdminId,
            adminFullName,
            request.MessageContent ?? string.Empty,
            request.IsInternalNote ?? false,
            attUrl,
            attName,
            cancellationToken);
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
            var roleInfo = await ResolveEmployeeRoleInfoAsync(systemAdminId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN") return null;

            var normalizedStatus = NormalizeStatus(request.Status);

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null) return null;

            if (!CanEmployeeViewTicket(ticket, systemAdminId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    var entity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
                    if (entity != null)
                    {
                        var oldStatus = entity.ticket_status;
                        entity.ticket_status = normalizedStatus;
                        if (!string.IsNullOrWhiteSpace(request.Resolution)) entity.ticket_resolution = request.Resolution;
                        if (normalizedStatus == "Resolved")
                        {
                            entity.ticket_resolved_at = DateTime.UtcNow;
                            entity.ticket_closed_at = DateTime.UtcNow;
                        }
                        entity.updated_at = DateTime.UtcNow;

                        var history = new TICKET_HISTORY
                        {
                            ticket_support_id = ticketId,
                            old_status = oldStatus,
                            new_status = normalizedStatus,
                            notes = request.Resolution,
                            changed_at = DateTime.UtcNow
                        };
                        db.TICKET_HISTORY.Add(history);

                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            ticket.TicketStatus = normalizedStatus;
            if (!string.IsNullOrWhiteSpace(request.Resolution))
            {
                ticket.TicketResolution = request.Resolution;
            }
            if (normalizedStatus == "Resolved")
            {
                ticket.TicketResolvedAt = DateTime.UtcNow;
                ticket.TicketClosedAt = DateTime.UtcNow;
            }
            ticket.UpdatedAt = DateTime.UtcNow;

            await SaveStateAsync(state, cancellationToken);
            return CloneTicketWithViewer(ticket, roleInfo.Role, systemAdminId, roleInfo.DepartmentId);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<(IReadOnlyList<AdminNotificationDto> Items, int UnreadCount)> GetAdminNotificationsAsync(
        int currentEmployeeId,
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            // System Admin KHÔNG nhận thông báo yêu cầu hỗ trợ
            if (roleInfo.Role == "SYSADMIN")
            {
                return ([], 0);
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var scopedList = FilterNotificationsForEmployee(state.AdminNotifications, state.Tickets, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId).ToList();

            IEnumerable<AdminNotificationDto> query = scopedList;
            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc == null);
            }

            var unreadCount = scopedList.Count(n => n.ReadAtUtc == null);
            var items = query.Take(Math.Max(1, Math.Min(limit, 100))).ToList();

            return (items, unreadCount);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> GetAdminUnreadNotificationCountAsync(int currentEmployeeId, CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN")
            {
                return 0;
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var scopedList = FilterNotificationsForEmployee(state.AdminNotifications, state.Tickets, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId);
            return scopedList.Count(n => n.ReadAtUtc == null);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<bool> MarkAdminNotificationReadAsync(int currentEmployeeId, int notificationId, CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN") return false;

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var scopedList = FilterNotificationsForEmployee(state.AdminNotifications, state.Tickets, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId);
            var notif = scopedList.FirstOrDefault(n => n.NotificationId == notificationId);
            if (notif is null) return false;

            notif.ReadAtUtc = DateTime.UtcNow;
            await SaveStateAsync(state, cancellationToken);
            return true;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> MarkAllAdminNotificationsReadAsync(int currentEmployeeId, CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            if (roleInfo.Role == "SYSADMIN") return 0;

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var scopedList = FilterNotificationsForEmployee(state.AdminNotifications, state.Tickets, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId);
            var count = 0;
            foreach (var n in scopedList.Where(n => n.ReadAtUtc == null))
            {
                n.ReadAtUtc = DateTime.UtcNow;
                count++;
            }
            if (count > 0)
            {
                await SaveStateAsync(state, cancellationToken);
            }
            return count;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<(string Role, int? DepartmentId, string? DepartmentName)> ResolveEmployeeRoleInfoAsync(
        int employeeId,
        CancellationToken ct = default)
    {
        if (employeeId <= 0)
        {
            return ("SYSADMIN", null, null);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();
            if (db == null)
            {
                return ("SYSADMIN", null, null);
            }

            await EnsureDatabaseTablesAsync(db, ct);

            var emp = await db.TblEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

            if (emp == null)
            {
                return ("SYSADMIN", null, null);
            }

            var empDeptMap = await LoadEmployeeDepartmentMapAsync(db, ct);
            var (role, deptId) = ClassifyEmployee(emp, empDeptMap);
            var deptName = deptId.HasValue ? GetDepartmentDisplayName(deptId.Value, null) : null;
            return (role, deptId, deptName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể xác định vai trò nhân viên {EmpId}", employeeId);
            return ("SYSADMIN", null, null);
        }
    }

    // ---------- Role & Department Scoping Helpers ----------

    private static IEnumerable<AdminNotificationDto> FilterNotificationsForEmployee(
        IEnumerable<AdminNotificationDto> notifications,
        IReadOnlyList<SupportTicketDto> tickets,
        int employeeId,
        string role,
        int? departmentId)
    {
        if (role == "SYSADMIN")
        {
            return [];
        }

        var ticketDict = tickets.ToDictionary(t => t.TicketSupportId);

        if (role == "DEPARTMENT_HEAD")
        {
            return notifications.Where(n =>
            {
                // Nếu thông báo chỉ định đích danh nhân viên khác (ví dụ thông báo giao việc cho nhân viên) thì không hiện cho Trưởng phòng
                if (n.TargetEmployeeId.HasValue && n.TargetEmployeeId.Value != employeeId && n.ActionKey == "assigned-ticket")
                {
                    return false;
                }

                if (n.TargetEmployeeId == employeeId) return true;
                if (departmentId.HasValue && n.DepartmentId == departmentId.Value) return true;

                if (ticketDict.TryGetValue(n.SubjectId, out var ticket))
                {
                    var ticketDeptId = ticket.DepartmentId ?? ResolveDepartmentIdFromInput(null, ticket.Department);
                    return departmentId.HasValue && ticketDeptId == departmentId.Value;
                }

                return false;
            });
        }

        if (role == "STAFF")
        {
            return notifications.Where(n =>
            {
                if (n.TargetEmployeeId == employeeId) return true;
                if (!n.TargetEmployeeId.HasValue && ticketDict.TryGetValue(n.SubjectId, out var ticket))
                {
                    return ticket.AssignedEmployeeId == employeeId
                        || ticket.Assignments.Any(a => a.EmployeeId == employeeId && a.AssignmentRole == "ASSIGNEE" && a.UnassignedAt == null);
                }
                return false;
            });
        }

        return [];
    }

    private static bool CanEmployeeViewTicket(
        SupportTicketDto t,
        int employeeId,
        string role,
        int? employeeDeptId)
    {
        if (employeeId <= 0 || role == "SYSADMIN")
        {
            return false;
        }

        if (role == "DEPARTMENT_HEAD")
        {
            if (!employeeDeptId.HasValue) return false;
            var ticketDeptId = t.DepartmentId ?? ResolveDepartmentIdFromInput(null, t.Department);
            return ticketDeptId == employeeDeptId.Value || t.DepartmentHeadEmployeeId == employeeId;
        }

        if (role == "STAFF")
        {
            if (t.AssignedEmployeeId == employeeId) return true;
            return t.Assignments.Any(a =>
                a.EmployeeId == employeeId
                && string.Equals(a.AssignmentRole, "ASSIGNEE", StringComparison.OrdinalIgnoreCase)
                && a.UnassignedAt == null);
        }

        return false;
    }

    private static bool BelongsToCustomerTenant(
        SupportTicketDto t,
        int tenantId,
        string tenantCode,
        int customerId)
    {
        // Khách hàng nào gửi yêu cầu thì CHỈ xem được yêu cầu do chính khách hàng đó gửi
        if (customerId > 0)
        {
            return t.CustomerId == customerId || t.CreatedByEmployeeId == customerId;
        }
        if (tenantId > 0 && t.TenantId == tenantId)
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(tenantCode) && string.Equals(t.TenantCode, tenantCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<SupportServiceItemDto>> GetAvailableServicesAsync(
        CancellationToken cancellationToken = default)
    {
        var defaultServices = new List<SupportServiceItemDto>
        {
            new() { ServiceId = 1, ServiceName = "Triển khai & Chuẩn hóa Mẫu hợp đồng", Description = "Thiết lập biểu mẫu và trường dữ liệu động" },
            new() { ServiceId = 2, ServiceName = "Gói Hỗ trợ Kỹ thuật Ưu tiên 24/7", Description = "Hỗ trợ kỹ thuật và bảo trì SLA 24/7" },
            new() { ServiceId = 3, ServiceName = "Phần mềm Quản lý Hợp đồng ConTrack Pro (SaaS)", Description = "Quản trị vòng đời hợp đồng điện tử" },
            new() { ServiceId = 4, ServiceName = "Dịch vụ Chữ ký số & Xác thực OTP (eSign CA)", Description = "Ký số hợp đồng và xác thực khách hàng" },
            new() { ServiceId = 5, ServiceName = "Server VPS / Hạ tầng Lưu trữ Hợp đồng Riêng", Description = "Máy chủ lưu trữ tài liệu và cơ sở dữ liệu riêng" },
            new() { ServiceId = 6, ServiceName = "Module Quét & Bóc tách Hợp đồng AI OCR", Description = "Nhận diện và số hóa văn bản hợp đồng PDF/Scan" }
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();
            if (db == null) return defaultServices;

            var dbServices = await db.SERVICES
                .AsNoTracking()
                .Where(s => s.Status == 1 || s.Status == null)
                .OrderBy(s => s.ServiceId)
                .Select(s => new SupportServiceItemDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName ?? $"Dịch vụ #{s.ServiceId}",
                    Description = s.MetaDescription ?? s.UnitName
                })
                .ToListAsync(cancellationToken);

            foreach (var def in defaultServices)
            {
                if (!dbServices.Any(x => x.ServiceId == def.ServiceId || string.Equals(x.ServiceName, def.ServiceName, StringComparison.OrdinalIgnoreCase)))
                {
                    dbServices.Add(def);
                }
            }

            return dbServices;
        }
        catch
        {
            return defaultServices;
        }
    }

    private static async Task<Dictionary<int, int>> LoadEmployeeDepartmentMapAsync(DbDtctechContext db, CancellationToken ct)
    {
        var map = new Dictionary<int, int>();
        try
        {
            var conn = db.Database.GetDbConnection();
            var shouldClose = conn.State != System.Data.ConnectionState.Open;
            if (shouldClose) await conn.OpenAsync(ct);
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT CAST([employee_id] AS INT), CAST([department_id] AS INT) FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] IS NOT NULL AND [department_id] IS NOT NULL";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var empId = reader.GetInt32(0);
                    var deptId = reader.GetInt32(1);
                    // Nếu nhân viên được gán vào phòng chuyên môn (2, 3, 4, 5), ưu tiên phòng chuyên môn hơn phòng 1 (Ban Giám Đốc)
                    if (!map.ContainsKey(empId) || (map[empId] == 1 && deptId > 1))
                    {
                        map[empId] = deptId;
                    }
                }
            }
            finally
            {
                if (shouldClose) await conn.CloseAsync();
            }
        }
        catch
        {
            // Fallback sẽ được xử lý trong ClassifyEmployee
        }
        return map;
    }

    private static (string Role, int? DepartmentId) ClassifyEmployee(TblEmployee emp, Dictionary<int, int> empDeptMap)
    {
        var account = (emp.EmployeeAccount ?? string.Empty).Trim().ToLowerInvariant();
        var code = (emp.EmployeeCode ?? string.Empty).Trim().ToUpperInvariant();
        var displayName = (emp.EmployeeFullName ?? string.Empty).Trim().ToLowerInvariant();

        // 1. System Admin: không tham gia luồng yêu cầu hỗ trợ
        if (account == "sysadmin" || account == "admin" || code == "SYS001" || code == "ADM-SYSADMIN" || displayName.Contains("system admin") || displayName.Contains("quản trị hệ thống"))
        {
            return ("SYSADMIN", null);
        }

        // 2. Xác định phòng ban của nhân viên
        int? deptId = empDeptMap.TryGetValue(emp.EmployeeId, out var mappedDept) && mappedDept > 1
            ? mappedDept
            : null;

        if (!deptId.HasValue)
        {
            if (account.Contains("tech") || account == "test.manager" || displayName.Contains("kỹ thuật"))
                deptId = 2;
            else if (account.Contains("sale") || displayName.Contains("kinh doanh") || displayName.Contains("bán hàng"))
                deptId = 3;
            else if (account.Contains("account") || displayName.Contains("kế toán") || displayName.Contains("tài chính"))
                deptId = 4;
            else if (account.Contains("legal") || account.Contains("hr") || displayName.Contains("pháp chế") || displayName.Contains("hành chính"))
                deptId = 5;
        }

        // 3. Xác định có phải Trưởng phòng hay Nhân viên
        var isHead = account.StartsWith("head.")
            || account.Contains("manager")
            || account.Contains("truongphong")
            || code.StartsWith("HEAD-")
            || code == "EMP-MANAGER"
            || displayName.Contains("trưởng phòng")
            || displayName.Contains("truong phong")
            || displayName.Contains("quản lý")
            || displayName.Contains("giám đốc");

        if (isHead && deptId.HasValue)
        {
            return ("DEPARTMENT_HEAD", deptId);
        }

        if (deptId.HasValue)
        {
            return ("STAFF", deptId);
        }

        return ("STAFF", 2);
    }

    private static async Task<TblEmployee?> FindDepartmentHeadAsync(DbDtctechContext db, int departmentId, CancellationToken ct)
    {
        var allEmps = await db.TblEmployees
            .AsNoTracking()
            .Where(e => e.Status == 1)
            .OrderBy(e => e.EmployeeId)
            .ToListAsync(ct);

        var empDeptMap = await LoadEmployeeDepartmentMapAsync(db, ct);

        foreach (var emp in allEmps)
        {
            var (role, deptId) = ClassifyEmployee(emp, empDeptMap);
            if (role == "DEPARTMENT_HEAD" && deptId == departmentId)
            {
                return emp;
            }
        }

        return null;
    }

    private static int ResolveDepartmentIdFromInput(int? departmentId, string? departmentText)
    {
        if (departmentId.HasValue && departmentId.Value is >= 2 and <= 5)
        {
            return departmentId.Value;
        }

        var d = (departmentText ?? string.Empty).Trim().ToUpperInvariant();
        if (d.Contains("BÁN HÀNG") || d.Contains("KINH DOANH") || d.Contains("SALE")) return 3;
        if (d.Contains("KẾ TOÁN") || d.Contains("TÀI CHÍNH") || d.Contains("ACCOUNT")) return 4;
        if (d.Contains("GÓP Ý") || d.Contains("PHÁP CHẾ") || d.Contains("HÀNH CHÍNH") || d.Contains("LEGAL")) return 5;
        return 2; // Mặc định: Phòng Kỹ Thuật & Hỗ Trợ (2)
    }

    private static string GetDepartmentDisplayName(int departmentId, string? fallback)
    {
        return departmentId switch
        {
            2 => "Phòng Kỹ Thuật & Hỗ Trợ",
            3 => "Phòng Kinh Doanh",
            4 => "Phòng Kế Toán - Tài Chính",
            5 => "Phòng Hành Chính - Pháp Chế",
            _ => !string.IsNullOrWhiteSpace(fallback) ? fallback : "Phòng Kỹ Thuật & Hỗ Trợ"
        };
    }

    // ---------- Helper methods & Db loaders ----------

    private async Task<List<SupportTicketDto>> LoadTicketsFromDbAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();
            if (db == null) return [];

            await EnsureDatabaseTablesAsync(db, ct);

            var ticketEntities = await db.TICKET_SUPPORT
                .AsNoTracking()
                .OrderByDescending(t => t.created_at)
                .Take(100)
                .ToListAsync(ct);

            if (ticketEntities.Count == 0) return [];

            var ticketIds = ticketEntities.Select(t => t.ticket_support_id).ToList();
            var messages = await db.TICKET_MESSAGE
                .AsNoTracking()
                .Where(m => m.ticket_support_id.HasValue && ticketIds.Contains(m.ticket_support_id.Value))
                .ToListAsync(ct);

            var messageIds = messages.Select(m => m.ticket_message_id).ToList();
            var attachments = await db.TICKET_ATTACHMENTS
                .AsNoTracking()
                .Where(a => a.ticket_message_id.HasValue && messageIds.Contains(a.ticket_message_id.Value))
                .ToListAsync(ct);

            var assignments = await db.TICKET_SUPPORT_EMPLOYEE
                .AsNoTracking()
                .Where(a => a.ticket_support_id.HasValue && ticketIds.Contains(a.ticket_support_id.Value))
                .ToListAsync(ct);

            var departments = await db.TblDepartments.AsNoTracking().ToDictionaryAsync(d => (long)d.DepartmentId, d => d.DepartmentName, ct);
            var employees = await db.TblEmployees.AsNoTracking().ToDictionaryAsync(e => (long)e.EmployeeId, ct);
            var services = await db.SERVICES.AsNoTracking().ToDictionaryAsync(s => (long)s.ServiceId, s => s.ServiceName ?? string.Empty, ct);
            var customers = await db.TblCustomers.AsNoTracking().ToDictionaryAsync(c => (long)c.CustomerId, ct);

            return ticketEntities.Select(e => MapEntityToDto(
                e,
                messages.Where(m => m.ticket_support_id == e.ticket_support_id).ToList(),
                attachments,
                assignments.Where(a => a.ticket_support_id == e.ticket_support_id).ToList(),
                departments,
                employees,
                services,
                customers)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể đọc tickets từ DbDtctechContext, sử dụng file cache.");
            return [];
        }
    }

    private async Task<SupportTicketDto?> LoadSingleTicketFromDbAsync(int ticketId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();
            if (db == null) return null;

            await EnsureDatabaseTablesAsync(db, ct);

            var entity = await db.TICKET_SUPPORT
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, ct);

            if (entity == null) return null;

            var messages = await db.TICKET_MESSAGE
                .AsNoTracking()
                .Where(m => m.ticket_support_id == ticketId)
                .ToListAsync(ct);

            var messageIds = messages.Select(m => m.ticket_message_id).ToList();
            var attachments = await db.TICKET_ATTACHMENTS
                .AsNoTracking()
                .Where(a => a.ticket_message_id.HasValue && messageIds.Contains(a.ticket_message_id.Value))
                .ToListAsync(ct);

            var assignments = await db.TICKET_SUPPORT_EMPLOYEE
                .AsNoTracking()
                .Where(a => a.ticket_support_id == ticketId)
                .ToListAsync(ct);

            var departments = await db.TblDepartments.AsNoTracking().ToDictionaryAsync(d => (long)d.DepartmentId, d => d.DepartmentName, ct);
            var employees = await db.TblEmployees.AsNoTracking().ToDictionaryAsync(e => (long)e.EmployeeId, ct);
            var services = await db.SERVICES.AsNoTracking().ToDictionaryAsync(s => (long)s.ServiceId, s => s.ServiceName ?? string.Empty, ct);
            var customers = await db.TblCustomers.AsNoTracking().ToDictionaryAsync(c => (long)c.CustomerId, ct);

            return MapEntityToDto(entity, messages, attachments, assignments, departments, employees, services, customers);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể đọc ticket {Id} từ DbDtctechContext.", ticketId);
            return null;
        }
    }

    private static SupportTicketDto MapEntityToDto(
        TICKET_SUPPORT e,
        IReadOnlyList<TICKET_MESSAGE> messages,
        IReadOnlyList<TICKET_ATTACHMENTS> attachments,
        IReadOnlyList<TICKET_SUPPORT_EMPLOYEE> assignments,
        Dictionary<long, string> departments,
        Dictionary<long, TblEmployee> employees,
        Dictionary<long, string>? services = null,
        Dictionary<long, TblCustomer>? customers = null)
    {
        var resolvedDeptId = e.department_id.HasValue ? (int)e.department_id.Value : 2;
        var deptName = e.department_id.HasValue && departments.TryGetValue(e.department_id.Value, out var dn)
            ? dn : GetDepartmentDisplayName(resolvedDeptId, null);

        string? serviceName = null;
        if (e.service_id.HasValue && services != null && services.TryGetValue(e.service_id.Value, out var sn) && !string.IsNullOrWhiteSpace(sn))
        {
            serviceName = sn;
        }

        TblCustomer? cust = e.customer_id.HasValue && customers != null && customers.TryGetValue(e.customer_id.Value, out var c) ? c : null;
        var customerDisplayName = cust?.CustomerFullName ?? cust?.CustomerCompany ?? cust?.CustomerEmail ?? "Khách hàng";
        var customerCompany = cust?.CustomerCompany ?? cust?.CustomerFullName ?? string.Empty;
        var customerCode = cust?.CustomerCode ?? string.Empty;

        var headAssignment = assignments
            .Where(a => a.ticket_support_employee_assignment_role == "DEPARTMENT_HEAD" && a.ticket_support_employee_unassigned_at == null)
            .OrderByDescending(a => a.ticket_support_employee_assigned_at)
            .FirstOrDefault()
            ?? assignments.FirstOrDefault(a => a.ticket_support_employee_assignment_role == "DEPARTMENT_HEAD");

        var staffAssignment = assignments
            .Where(a => a.ticket_support_employee_assignment_role == "ASSIGNEE" && a.ticket_support_employee_unassigned_at == null)
            .OrderByDescending(a => a.ticket_support_employee_assigned_at)
            .FirstOrDefault();

        TblEmployee? headEmp = headAssignment?.employee_id != null && employees.TryGetValue(headAssignment.employee_id.Value, out var h) ? h : null;
        TblEmployee? staffEmp = staffAssignment?.employee_id != null && employees.TryGetValue(staffAssignment.employee_id.Value, out var s) ? s : null;

        var attachmentDict = attachments
            .Where(a => a.ticket_message_id.HasValue)
            .GroupBy(a => a.ticket_message_id!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new SupportTicketDto
        {
            TicketSupportId = (int)e.ticket_support_id,
            CustomerId = (int?)e.customer_id,
            TenantId = (int?)e.tenant_id,
            TenantName = customerCompany,
            TenantCode = customerCode,
            CreatedByEmployeeId = (int?)e.customer_id,
            CreatedByName = customerDisplayName,
            DepartmentId = resolvedDeptId,
            Department = deptName,
            ServiceId = e.service_id,
            ServiceName = serviceName,
            Module = serviceName ?? "Dịch vụ chung",
            TicketCode = e.ticket_code ?? $"TK-{e.ticket_support_id}",
            TicketSubject = e.ticket_title ?? string.Empty,
            TicketDescription = e.ticket_description ?? string.Empty,
            TicketPriority = NormalizePriority(e.ticket_priority),
            TicketStatus = NormalizeStatus(e.ticket_status),
            TicketResolution = e.ticket_resolution,
            TicketResolvedAt = e.ticket_resolved_at,
            TicketClosedAt = e.ticket_closed_at,
            CreatedAt = e.created_at,
            UpdatedAt = e.updated_at,
            DepartmentHeadEmployeeId = headEmp != null ? headEmp.EmployeeId : (int?)headAssignment?.employee_id,
            DepartmentHeadName = headEmp != null ? (headEmp.EmployeeFullName ?? headEmp.EmployeeAccount) : null,
            AssignedEmployeeId = staffEmp != null ? staffEmp.EmployeeId : (int?)staffAssignment?.employee_id,
            AssignedEmployeeName = staffEmp != null ? (staffEmp.EmployeeFullName ?? staffEmp.EmployeeAccount) : null,
            AssignedAt = staffAssignment?.ticket_support_employee_assigned_at,
            Assignments = assignments.Select(a =>
            {
                var emp = a.employee_id.HasValue ? employees.GetValueOrDefault(a.employee_id.Value) : null;
                var assigner = a.ticket_support_employee_assigned_by_employee_id.HasValue
                    ? employees.GetValueOrDefault(a.ticket_support_employee_assigned_by_employee_id.Value) : null;
                return new TicketEmployeeAssignmentDto
                {
                    TicketSupportEmployeeId = a.ticket_support_employee_id,
                    TicketSupportId = a.ticket_support_id ?? e.ticket_support_id,
                    EmployeeId = a.employee_id.HasValue ? (int)a.employee_id.Value : 0,
                    EmployeeName = emp?.EmployeeFullName ?? emp?.EmployeeAccount ?? $"NV #{a.employee_id}",
                    EmployeeCode = emp?.EmployeeCode,
                    EmployeeEmail = emp?.EmployeeEmail,
                    AssignmentRole = a.ticket_support_employee_assignment_role ?? string.Empty,
                    AssignedAt = a.ticket_support_employee_assigned_at,
                    UnassignedAt = a.ticket_support_employee_unassigned_at,
                    AssignedByEmployeeId = (int?)a.ticket_support_employee_assigned_by_employee_id,
                    AssignedByName = assigner?.EmployeeFullName ?? assigner?.EmployeeAccount
                };
            }).ToList(),
            Messages = messages.OrderBy(m => m.created_at).Select(m =>
            {
                var msgAtts = attachmentDict.GetValueOrDefault(m.ticket_message_id);
                var firstAtt = msgAtts?.FirstOrDefault();
                var senderCust = m.ticket_message_sender_customer_id.HasValue && customers != null && customers.TryGetValue(m.ticket_message_sender_customer_id.Value, out var sc) ? sc : cust;
                return new SupportTicketMessageDto
                {
                    TicketMessageId = (int)m.ticket_message_id,
                    TicketSupportId = (int)(m.ticket_support_id ?? e.ticket_support_id),
                    SenderCustomerId = (int?)m.ticket_message_sender_customer_id,
                    SenderEmployeeId = (int?)m.ticket_message_sender_employee_id,
                    SenderName = m.ticket_message_sender_customer_id.HasValue
                        ? (senderCust?.CustomerFullName ?? senderCust?.CustomerCompany ?? "Khách hàng")
                        : (m.ticket_message_sender_employee_id.HasValue && employees.TryGetValue(m.ticket_message_sender_employee_id.Value, out var se)
                            ? (se.EmployeeFullName ?? se.EmployeeAccount ?? "Nhân viên hỗ trợ")
                            : "Hệ thống hỗ trợ"),
                    IsFromEmployee = m.ticket_message_sender_employee_id.HasValue,
                    TicketMessageContent = m.ticket_message_content ?? string.Empty,
                    TicketMessageType = m.ticket_message_type ?? "TEXT",
                    TicketMessageAttachmentUrl = firstAtt?.file_name_1,
                    TicketMessageAttachmentName = firstAtt?.file_name_2,
                    CreatedAt = m.created_at ?? DateTime.UtcNow
                };
            }).ToList()
        };
    }

    private static List<SupportTicketDto> MergeTickets(List<SupportTicketDto> fromDb, List<SupportTicketDto> fromState)
    {
        var dict = new Dictionary<string, SupportTicketDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in fromState)
        {
            if (!string.IsNullOrWhiteSpace(t.TicketCode)) dict[t.TicketCode] = t;
        }
        foreach (var t in fromDb)
        {
            if (!string.IsNullOrWhiteSpace(t.TicketCode))
            {
                if (dict.TryGetValue(t.TicketCode, out var existing))
                {
                    // Giữ lại thông tin tên khách hàng / tenant / hình ảnh đính kèm từ state nếu DB không lưu trực tiếp
                    if (string.IsNullOrWhiteSpace(t.TenantName) && !string.IsNullOrWhiteSpace(existing.TenantName))
                        t.TenantName = existing.TenantName;
                    if (string.IsNullOrWhiteSpace(t.TenantCode) && !string.IsNullOrWhiteSpace(existing.TenantCode))
                        t.TenantCode = existing.TenantCode;
                    if (string.IsNullOrWhiteSpace(t.CreatedByName) && !string.IsNullOrWhiteSpace(existing.CreatedByName))
                        t.CreatedByName = existing.CreatedByName;
                    if (!t.CreatedByEmployeeId.HasValue && existing.CreatedByEmployeeId.HasValue)
                        t.CreatedByEmployeeId = existing.CreatedByEmployeeId;
                    if (!t.ServiceId.HasValue && existing.ServiceId.HasValue)
                        t.ServiceId = existing.ServiceId;
                    if (string.IsNullOrWhiteSpace(t.ServiceName) && !string.IsNullOrWhiteSpace(existing.ServiceName))
                        t.ServiceName = existing.ServiceName;
                    if ((string.IsNullOrWhiteSpace(t.Module) || t.Module == "Dịch vụ chung") && !string.IsNullOrWhiteSpace(existing.Module))
                        t.Module = existing.Module;
                    if (t.AttachmentUrls.Count == 0 && existing.AttachmentUrls.Count > 0)
                        t.AttachmentUrls = [.. existing.AttachmentUrls];
                    if (t.AttachmentNames.Count == 0 && existing.AttachmentNames.Count > 0)
                        t.AttachmentNames = [.. existing.AttachmentNames];

                    // Đồng bộ hình ảnh đính kèm (Data URL) trong Messages từ state sang DB messages
                    if (existing.Messages.Count > 0)
                    {
                        for (int i = 0; i < t.Messages.Count && i < existing.Messages.Count; i++)
                        {
                            var dbMsg = t.Messages[i];
                            var stMsg = existing.Messages[i];
                            if ((string.IsNullOrWhiteSpace(dbMsg.TicketMessageAttachmentUrl) || !dbMsg.TicketMessageAttachmentUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                                && !string.IsNullOrWhiteSpace(stMsg.TicketMessageAttachmentUrl))
                            {
                                dbMsg.TicketMessageAttachmentUrl = stMsg.TicketMessageAttachmentUrl;
                            }
                            if (string.IsNullOrWhiteSpace(dbMsg.TicketMessageAttachmentName) && !string.IsNullOrWhiteSpace(stMsg.TicketMessageAttachmentName))
                            {
                                dbMsg.TicketMessageAttachmentName = stMsg.TicketMessageAttachmentName;
                            }
                            if (dbMsg.AttachmentUrls.Count == 0 && stMsg.AttachmentUrls.Count > 0)
                            {
                                dbMsg.AttachmentUrls = [.. stMsg.AttachmentUrls];
                            }
                            if (dbMsg.AttachmentNames.Count == 0 && stMsg.AttachmentNames.Count > 0)
                            {
                                dbMsg.AttachmentNames = [.. stMsg.AttachmentNames];
                            }
                        }

                        // Nếu state có nhiều tin nhắn hơn DB (tin nhắn vừa gửi kèm ảnh), bổ sung các tin nhắn còn thiếu
                        if (existing.Messages.Count > t.Messages.Count)
                        {
                            for (int i = t.Messages.Count; i < existing.Messages.Count; i++)
                            {
                                t.Messages.Add(existing.Messages[i]);
                            }
                        }
                    }
                }
                dict[t.TicketCode] = t;
            }
        }
        return dict.Values.ToList();
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
            var targetDeptId = ResolveDepartmentIdFromInput(null, department);
            query = query.Where(t => (t.DepartmentId ?? ResolveDepartmentIdFromInput(null, t.Department)) == targetDeptId);
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
                || (t.CreatedByName != null && t.CreatedByName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (t.ServiceName != null && t.ServiceName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (t.Module != null && t.Module.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || t.Department.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Open";
        return status.Trim().ToLowerInvariant() switch
        {
            "in_progress" or "inprogress" or "pending_customer" or "inprocessing" => "In_Progress",
            "pendingheadapproval" or "pending_head_approval" => "PendingHeadApproval",
            "resolved" or "closed" or "completed" => "Resolved",
            _ => "Open"
        };
    }

    private static string NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return "Medium";
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
        if (_cachedState is not null) return _cachedState;

        if (File.Exists(_storeFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_storeFilePath, cancellationToken);
                var loaded = JsonSerializer.Deserialize<SupportTicketStoreState>(json, JsonOptions);
                if (loaded is not null)
                {
                    _cachedState = loaded;
                    return _cachedState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc file JSON lưu trữ, khởi tạo lại.");
            }
        }

        _cachedState = new SupportTicketStoreState();
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
            _logger.LogWarning(ex, "Không thể ghi file lưu trữ SupportTicket.");
        }
    }

    private static SupportTicketDto CloneTicketWithViewer(
        SupportTicketDto src,
        string viewerRole,
        int? viewerEmployeeId,
        int? viewerDepartmentId)
    {
        var copy = CloneTicket(src);
        copy.ViewerRole = viewerRole;
        copy.ViewerEmployeeId = viewerEmployeeId;
        copy.ViewerDepartmentId = viewerDepartmentId;
        return copy;
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
        DepartmentId = src.DepartmentId,
        Department = src.Department,
        ServiceId = src.ServiceId,
        ServiceName = src.ServiceName,
        Module = src.Module,
        TicketResolution = src.TicketResolution,
        TicketResolvedAt = src.TicketResolvedAt,
        TicketClosedAt = src.TicketClosedAt,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        DepartmentHeadEmployeeId = src.DepartmentHeadEmployeeId,
        DepartmentHeadName = src.DepartmentHeadName,
        AssignedEmployeeId = src.AssignedEmployeeId,
        AssignedEmployeeName = src.AssignedEmployeeName,
        AssignedAt = src.AssignedAt,
        ViewerRole = src.ViewerRole,
        ViewerEmployeeId = src.ViewerEmployeeId,
        ViewerDepartmentId = src.ViewerDepartmentId,
        AttachmentUrls = [.. src.AttachmentUrls],
        AttachmentNames = [.. src.AttachmentNames],
        Assignments = src.Assignments.Select(a => new TicketEmployeeAssignmentDto
        {
            TicketSupportEmployeeId = a.TicketSupportEmployeeId,
            TicketSupportId = a.TicketSupportId,
            EmployeeId = a.EmployeeId,
            EmployeeName = a.EmployeeName,
            EmployeeCode = a.EmployeeCode,
            EmployeeEmail = a.EmployeeEmail,
            AssignmentRole = a.AssignmentRole,
            AssignedAt = a.AssignedAt,
            UnassignedAt = a.UnassignedAt,
            AssignedByEmployeeId = a.AssignedByEmployeeId,
            AssignedByName = a.AssignedByName
        }).ToList(),
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
            AttachmentUrls = [.. m.AttachmentUrls],
            AttachmentNames = [.. m.AttachmentNames],
            CreatedAt = m.CreatedAt
        }).ToList()
    };

    // ---------- Notifications (Strict Department Head & Assigned Staff Only; Never SysAdmin) ----------

    private async Task SendNewTicketNotificationAsync(SupportTicketDto ticket, string senderName, TblEmployee? head)
    {
        try
        {
            // Chỉ gửi thông báo cho Trưởng phòng của phòng ban được khách hàng chọn (không gửi cho sysadmin)
            if (head == null || !SmtpEmailOptions.IsEmailAddress(head.EmployeeEmail))
            {
                return;
            }

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildNewTicketEmail(
                head.EmployeeEmail!,
                ticket,
                senderName,
                "Không có tệp đính kèm",
                systemAdminUrl);

            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xử lý gửi email thông báo ticket mới {Code} tới Trưởng phòng.", ticket.TicketCode);
        }
    }

    private async Task SendStaffAssignmentNotificationAsync(SupportTicketDto ticket, TblEmployee staff, string headName, string? note)
    {
        try
        {
            if (staff.EmployeeEmail != null && SmtpEmailOptions.IsEmailAddress(staff.EmployeeEmail))
            {
                var subject = $"[ConTrack Support] Bạn được phân công xử lý Ticket {ticket.TicketCode}";
                var textBody = $"Trưởng phòng {headName} đã phân công bạn xử lý Ticket {ticket.TicketCode}: {ticket.TicketSubject}";
                var htmlBody = $@"<div style='font-family: Arial, sans-serif; padding: 20px;'>
                        <h2>Phân công công việc hỗ trợ</h2>
                        <p>Xin chào <strong>{staff.EmployeeFullName}</strong>,</p>
                        <p>Trưởng phòng <strong>{headName}</strong> đã phân công bạn xử lý Ticket hỗ trợ sau:</p>
                        <ul>
                            <li><strong>Mã Ticket:</strong> {ticket.TicketCode}</li>
                            <li><strong>Tiêu đề:</strong> {ticket.TicketSubject}</li>
                            <li><strong>Khách hàng:</strong> {ticket.CreatedByName} ({ticket.TenantName})</li>
                            <li><strong>Mức độ ưu tiên:</strong> {ticket.TicketPriority}</li>
                            <li><strong>Ghi chú phân công:</strong> {note ?? "Không có"}</li>
                        </ul>
                        <p>Vui lòng đăng nhập vào hệ thống để tiếp nhận và xử lý.</p>
                    </div>";
                var emailMsg = new EmailMessage(staff.EmployeeEmail, subject, textBody, htmlBody);
                await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi email phân công cho nhân viên {EmpId}.", staff.EmployeeId);
        }
    }

    private async Task SendResolutionNoticeToCustomerAsync(SupportTicketDto ticket, string resolutionNotice)
    {
        try
        {
            var customerEmail = _configuration["SupportTicket:CustomerNotificationEmail"];
            if (string.IsNullOrWhiteSpace(customerEmail)) return;

            var subject = $"[ConTrack Support] Yêu cầu hỗ trợ {ticket.TicketCode} đã được giải quyết";
            var textBody = $"Yêu cầu hỗ trợ {ticket.TicketCode} của bạn đã được giải quyết: {resolutionNotice}";
            var htmlBody = $@"<div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2>Thông báo kết quả xử lý yêu cầu hỗ trợ</h2>
                    <p>Xin chào <strong>{ticket.CreatedByName}</strong>,</p>
                    <p>Yêu cầu hỗ trợ <strong>{ticket.TicketCode}</strong> của bạn đã được xử lý:</p>
                    <div style='background: #f4f8fa; padding: 15px; border-left: 4px solid #0284c7; margin: 15px 0;'>
                        {resolutionNotice}
                    </div>
                    <p>Nếu bạn có thêm câu hỏi, bạn có thể tiếp tục trao đổi trên hệ thống.</p>
                </div>";
            var emailMsg = new EmailMessage(customerEmail, subject, textBody, htmlBody);
            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi email thông báo kết quả cho khách hàng.");
        }
    }

    private async Task SendCustomerReplyEmailsSafeAsync(SupportTicketDto ticket, SupportTicketMessageDto message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();
            if (db == null) return;

            // Chỉ gửi email cho nhân viên đang phụ trách ticket hoặc Trưởng phòng của phòng ban đó (không gửi cho sysadmin)
            var targetEmpId = ticket.AssignedEmployeeId ?? ticket.DepartmentHeadEmployeeId;
            if (!targetEmpId.HasValue) return;

            var emp = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == targetEmpId.Value);
            if (emp?.EmployeeEmail == null || !SmtpEmailOptions.IsEmailAddress(emp.EmployeeEmail)) return;

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildCustomerReplyEmail(emp.EmployeeEmail, ticket, message, systemAdminUrl);
            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi email phản hồi ticket {Code}.", ticket.TicketCode);
        }
    }

    private string BuildSystemAdminTicketUrl(int ticketSupportId)
    {
        var baseUrl = _configuration["SupportTicket:SystemAdminBaseUrl"]
            ?? _configuration["AccountRecovery:SystemAdminFrontendBaseUrl"]
            ?? "http://localhost:3001";

        return $"{baseUrl.TrimEnd('/')}/support-tickets/{ticketSupportId}";
    }
}
