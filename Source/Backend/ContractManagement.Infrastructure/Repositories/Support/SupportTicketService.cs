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
        string? status,
        string? priority,
        string? department,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            // Thử đọc từ Database EF Core trước
            var dbTickets = await LoadTicketsFromDbAsync(cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);

            var combined = MergeTickets(dbTickets, state.Tickets);
            IEnumerable<SupportTicketDto> query = combined.OrderByDescending(t => t.CreatedAt);

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
            var dbTickets = await LoadTicketsFromDbAsync(cancellationToken);
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var combined = MergeTickets(dbTickets, state.Tickets);

            // Xác định vai trò của employeeId hiện tại
            var isHead = false;
            int? employeeDeptId = null;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null && employeeId > 0)
                {
                    var emp = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId, cancellationToken);
                    if (emp != null)
                    {
                        employeeDeptId = emp.DepartmentId;
                        isHead = emp.EmployeeType == (byte)EmployeeType.Manager;
                    }
                }
            }

            IEnumerable<SupportTicketDto> query = combined
                .Where(t => BelongsToViewer(t, tenantId, tenantCode, employeeId, isHead, employeeDeptId))
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
            var fromDb = await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (fromDb != null) return CloneTicket(fromDb);

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
            var ticket = await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket == null)
            {
                var state = await EnsureStateLoadedAsync(cancellationToken);
                ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            }

            if (ticket is null) return null;

            var isHead = false;
            int? employeeDeptId = null;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null && employeeId > 0)
                {
                    var emp = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId, cancellationToken);
                    if (emp != null)
                    {
                        employeeDeptId = emp.DepartmentId;
                        isHead = emp.EmployeeType == (byte)EmployeeType.Manager;
                    }
                }
            }

            if (!BelongsToViewer(ticket, tenantId, tenantCode, employeeId, isHead, employeeDeptId))
            {
                return null;
            }

            return CloneTicket(ticket);
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

            TblDepartment? dept = null;
            TblEmployee? head = null;
            var ticketCode = $"TK-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    await EnsureDatabaseTablesAsync(db, cancellationToken);

                    // 1. Tìm phòng ban theo ID hoặc tên/mã
                    if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
                    {
                        dept = await db.TblDepartments.AsNoTracking()
                            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId.Value, cancellationToken);
                    }
                    if (dept == null && !string.IsNullOrWhiteSpace(request.Department))
                    {
                        var deptName = request.Department.Trim();
                        dept = await db.TblDepartments.AsNoTracking()
                            .FirstOrDefaultAsync(d => d.DepartmentName == deptName || d.DepartmentCode == deptName, cancellationToken);
                    }

                    // 2. NGHIỆP VỤ: Bên nhận ban đầu là NGƯỜI ĐỨNG ĐẦU CỦA PHÒNG BAN ĐÓ (Manager / Trưởng phòng)
                    if (dept != null)
                    {
                        // Tìm Manager của phòng ban
                        head = await db.TblEmployees.AsNoTracking()
                            .FirstOrDefaultAsync(e => e.DepartmentId == dept.DepartmentId && e.EmployeeType == (byte)EmployeeType.Manager && e.Status == 1, cancellationToken);

                        // Nếu chưa set Manager cụ thể, lấy nhân viên đầu tiên thuộc phòng ban đó
                        head ??= await db.TblEmployees.AsNoTracking()
                            .FirstOrDefaultAsync(e => e.DepartmentId == dept.DepartmentId && e.Status == 1, cancellationToken);
                    }

                    // Fallback nếu không có nhân viên nào trong phòng ban: lấy bất kỳ Manager nào của hệ thống
                    head ??= await db.TblEmployees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.EmployeeType == (byte)EmployeeType.Manager && e.Status == 1, cancellationToken);

                    // 3. Lưu vào Database Table TICKET_SUPPORT
                    try
                    {
                        var entity = new TICKET_SUPPORT
                        {
                            customer_id = employeeId > 0 ? employeeId : 1,
                            tenant_id = tenantId > 0 ? tenantId : 1,
                            department_id = dept?.DepartmentId,
                            product_id = request.ProductId,
                            service_id = request.ServiceId,
                            ticket_code = ticketCode,
                            ticket_title = request.ResolvedSubject,
                            ticket_description = request.ResolvedDescription,
                            ticket_priority = NormalizePriority(request.ResolvedPriority),
                            ticket_status = "Open",
                            created_at = DateTime.UtcNow
                        };

                        db.TICKET_SUPPORT.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);

                        // 4. Lưu vào TICKET_SUPPORT_EMPLOYEE với role = DEPARTMENT_HEAD
                        if (head != null)
                        {
                            var headAssignment = new TICKET_SUPPORT_EMPLOYEE
                            {
                                ticket_support_id = entity.ticket_support_id,
                                employee_id = head.EmployeeId,
                                ticket_support_employee_assignment_role = "DEPARTMENT_HEAD",
                                ticket_support_employee_assigned_at = DateTime.UtcNow,
                                ticket_support_employee_assigned_by_employee_id = null // Tự động gán cho Trưởng phòng
                            };
                            db.TICKET_SUPPORT_EMPLOYEE.Add(headAssignment);
                        }

                        // 5. Lưu tin nhắn mở đầu vào TICKET_MESSAGE
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

                        if (!string.IsNullOrWhiteSpace(request.AttachmentUrls?.FirstOrDefault()))
                        {
                            var initAtt = new TICKET_ATTACHMENTS
                            {
                                ticket_message_id = initMsg.ticket_message_id,
                                file_name_1 = request.AttachmentUrls?.FirstOrDefault(),
                                file_name_2 = request.AttachmentNames?.FirstOrDefault(),
                                uploaded_at = DateTime.UtcNow
                            };
                            db.TICKET_ATTACHMENTS.Add(initAtt);
                        }

                        // 6. Ghi nhật ký vào TICKET_HISTORY
                        var initHistory = new TICKET_HISTORY
                        {
                            ticket_support_id = entity.ticket_support_id,
                            employee_id_start = head != null ? (int)head.EmployeeId : null,
                            old_status = null,
                            new_status = "Open",
                            notes = $"Khởi tạo phiếu hỗ trợ bởi {senderName}.",
                            changed_at = DateTime.UtcNow
                        };
                        db.TICKET_HISTORY.Add(initHistory);

                        await db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Đã lưu Ticket {TicketCode} vào database thành công với Trưởng phòng nhận {HeadName}.",
                            ticketCode, head?.EmployeeFullName ?? "Chưa xác định");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lưu vào database thất bại, sử dụng backup store.");
                    }
                }
            }

            var ticketId = state.NextTicketId++;
            var departmentDisplayName = dept?.DepartmentName ?? request.Department ?? "KỸ THUẬT";

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
                DepartmentId = dept?.DepartmentId,
                Department = departmentDisplayName,
                Module = request.Module ?? "Chung",
                CreatedAt = DateTime.UtcNow,
                DepartmentHeadEmployeeId = head?.EmployeeId,
                DepartmentHeadName = head?.EmployeeFullName ?? head?.EmployeeAccount,
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
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            };

            state.Tickets.Insert(0, ticket);

            // Thêm Notification cho Trưởng phòng / Quản trị viên
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
                Department = ticket.Department,
                ActionKey = "new-ticket",
                CreatedAtUtc = DateTime.UtcNow
            };
            state.AdminNotifications.Insert(0, notification);

            await SaveStateAsync(state, cancellationToken);

            // Gửi email thông báo cho Trưởng phòng nhận việc
            _ = SendNewTicketNotificationAsync(ticket, senderName, head);

            return CloneTicket(ticket);
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

        // Tìm ticket để lấy DepartmentId
        int? deptId = null;
        var ticket = await db.TICKET_SUPPORT.AsNoTracking().FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
        if (ticket != null)
        {
            deptId = (int?)ticket.department_id;
        }

        if (!deptId.HasValue)
        {
            var emp = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == currentEmployeeId, cancellationToken);
            deptId = emp?.DepartmentId;
        }

        if (!deptId.HasValue) return [];

        var dept = await db.TblDepartments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == deptId.Value, cancellationToken);
        var staffList = await db.TblEmployees.AsNoTracking()
            .Where(e => e.DepartmentId == deptId.Value && e.Status == 1)
            .OrderBy(e => e.EmployeeFullName)
            .ToListAsync(cancellationToken);

        return staffList.Select(e => new DepartmentEmployeeDto
        {
            EmployeeId = e.EmployeeId,
            EmployeeName = !string.IsNullOrWhiteSpace(e.EmployeeFullName) ? e.EmployeeFullName : (e.EmployeeAccount ?? $"NV #{e.EmployeeId}"),
            EmployeeCode = e.EmployeeCode,
            EmployeeEmail = e.EmployeeEmail,
            DepartmentId = e.DepartmentId,
            DepartmentName = dept?.DepartmentName,
            IsManager = e.EmployeeType == (byte)EmployeeType.Manager
        }).ToList();
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();

            TblEmployee? staff = null;
            if (db != null)
            {
                staff = await db.TblEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == request.StaffEmployeeId, cancellationToken);
                var ticketEntity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);

                if (ticketEntity != null && staff != null)
                {
                    ticketEntity.ticket_status = "In_Progress";
                    ticketEntity.updated_at = DateTime.UtcNow;

                    // Thêm bản ghi phân công TICKET_SUPPORT_EMPLOYEE
                    var assignment = new TICKET_SUPPORT_EMPLOYEE
                    {
                        ticket_support_id = ticketId,
                        employee_id = staff.EmployeeId,
                        ticket_support_employee_assignment_role = "ASSIGNEE",
                        ticket_support_employee_assigned_at = DateTime.UtcNow,
                        ticket_support_employee_assigned_by_employee_id = currentEmployeeId
                    };
                    db.TICKET_SUPPORT_EMPLOYEE.Add(assignment);

                    // Thêm bản ghi lịch sử TICKET_HISTORY
                    var assignHistory = new TICKET_HISTORY
                    {
                        ticket_support_id = ticketId,
                        employee_id_start = currentEmployeeId,
                        employee_id_finished = (int)staff.EmployeeId,
                        changed_by_user_id = currentEmployeeId,
                        old_status = "Open",
                        new_status = "In_Progress",
                        notes = $"Trưởng phòng {currentEmployeeName} phân công cho nhân viên {staff.EmployeeFullName}. Ghi chú: {request.Note}",
                        changed_at = DateTime.UtcNow
                    };
                    db.TICKET_HISTORY.Add(assignHistory);

                    // Thêm tin nhắn hệ thống ghi nhận phân công
                    var msg = new TICKET_MESSAGE
                    {
                        ticket_support_id = ticketId,
                        ticket_message_sender_employee_id = currentEmployeeId,
                        ticket_message_type = "ASSIGNMENT",
                        ticket_message_content = $"[Phân công việc] Trưởng phòng {currentEmployeeName} đã phân công xử lý cho nhân viên {staff.EmployeeFullName}. Ghi chú: {request.Note}",
                        created_at = DateTime.UtcNow
                    };
                    db.TICKET_MESSAGE.Add(msg);

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            // Đồng bộ bộ nhớ / JSON file
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket != null)
            {
                var staffName = staff?.EmployeeFullName ?? $"Nhân viên #{request.StaffEmployeeId}";
                ticket.TicketStatus = "In_Progress";
                ticket.AssignedEmployeeId = request.StaffEmployeeId;
                ticket.AssignedEmployeeName = staffName;
                ticket.AssignedAt = DateTime.UtcNow;
                ticket.UpdatedAt = DateTime.UtcNow;

                ticket.Assignments.Add(new TicketEmployeeAssignmentDto
                {
                    TicketSupportEmployeeId = ticket.Assignments.Count + 1,
                    TicketSupportId = ticketId,
                    EmployeeId = request.StaffEmployeeId,
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
                    TicketMessageContent = $"[Phân công việc] Trưởng phòng {currentEmployeeName} đã phân công xử lý cho nhân viên {staffName}. Ghi chú: {request.Note}",
                    TicketMessageType = "ASSIGNMENT",
                    CreatedAt = DateTime.UtcNow
                });

                await SaveStateAsync(state, cancellationToken);
            }

            // Gửi email thông báo cho nhân viên được giao việc
            if (staff != null && ticket != null)
            {
                _ = SendStaffAssignmentNotificationAsync(ticket, staff, currentEmployeeName, request.Note);
            }

            return ticket is null ? null : CloneTicket(ticket);
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
                        ticket_message_content = $"[Hoàn tất xử lý] Nhân viên {currentEmployeeName} đã xử lý xong: {request.Resolution}. Báo cáo Trưởng phòng kiểm tra và phản hồi kết quả cho khách hàng.",
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
                        notes = $"Nhân viên {currentEmployeeName} báo cáo hoàn tất: {request.Resolution}",
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
                    TicketMessageContent = $"[Hoàn tất xử lý] Nhân viên {currentEmployeeName} đã xử lý xong: {request.Resolution}. Báo cáo Trưởng phòng kiểm tra và phản hồi kết quả cho khách hàng.",
                    TicketMessageType = "STAFF_COMPLETION",
                    CreatedAt = DateTime.UtcNow
                });

                await SaveStateAsync(state, cancellationToken);
            }

            return ticket is null ? null : CloneTicket(ticket);
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
            var targetStatus = request.CloseTicket ? "Resolved" : "In_Progress";
            var resolutionText = request.Resolution;
            var customerNotice = !string.IsNullOrWhiteSpace(request.MessageToCustomer)
                ? request.MessageToCustomer
                : $"[Kết quả giải quyết từ Trưởng phòng {currentEmployeeName}]: {resolutionText}";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<DbDtctechContext>();

            if (db != null)
            {
                var ticketEntity = await db.TICKET_SUPPORT.FirstOrDefaultAsync(t => t.ticket_support_id == ticketId, cancellationToken);
                if (ticketEntity != null)
                {
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
                        old_status = "PendingHeadApproval",
                        new_status = targetStatus,
                        notes = $"Trưởng phòng {currentEmployeeName} thông báo kết quả: {customerNotice}",
                        changed_at = DateTime.UtcNow
                    };
                    db.TICKET_HISTORY.Add(headHistory);

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket != null)
            {
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

                await SaveStateAsync(state, cancellationToken);

                // Gửi email thông báo kết quả giải quyết cho khách hàng
                _ = SendResolutionNoticeToCustomerAsync(ticket, customerNotice);
            }

            return ticket is null ? null : CloneTicket(ticket);
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
            var content = (request.MessageContent ?? request.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content)) return null;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
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

                    if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
                    {
                        var att = new TICKET_ATTACHMENTS
                        {
                            ticket_message_id = entity.ticket_message_id,
                            file_name_1 = request.AttachmentUrl,
                            file_name_2 = request.AttachmentName,
                            uploaded_at = DateTime.UtcNow
                        };
                        db.TICKET_ATTACHMENTS.Add(att);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
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
                TicketMessageAttachmentName = request.AttachmentName,
                TicketMessageAttachmentUrl = request.AttachmentUrl,
                CreatedAt = DateTime.UtcNow
            };

            ticket.Messages.Add(msg);
            ticket.UpdatedAt = DateTime.UtcNow;

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
            if (string.IsNullOrWhiteSpace(content)) return null;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<DbDtctechContext>();
                if (db != null)
                {
                    var entity = new TICKET_MESSAGE
                    {
                        ticket_support_id = ticketId,
                        ticket_message_sender_employee_id = employeeId,
                        ticket_message_content = content,
                        ticket_message_type = isInternalNote ? "INTERNAL_NOTE" : "STAFF_REPLY",
                        created_at = DateTime.UtcNow
                    };
                    db.TICKET_MESSAGE.Add(entity);
                    await db.SaveChangesAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(attachmentUrl))
                    {
                        var att = new TICKET_ATTACHMENTS
                        {
                            ticket_message_id = entity.ticket_message_id,
                            file_name_1 = attachmentUrl,
                            file_name_2 = attachmentName,
                            uploaded_at = DateTime.UtcNow
                        };
                        db.TICKET_ATTACHMENTS.Add(att);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null) return null;

            var msg = new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderEmployeeId = employeeId,
                SenderName = employeeName,
                IsFromEmployee = true,
                TicketMessageContent = content,
                TicketMessageType = isInternalNote ? "INTERNAL_NOTE" : "STAFF_REPLY",
                TicketMessageAttachmentName = attachmentName,
                TicketMessageAttachmentUrl = attachmentUrl,
                CreatedAt = DateTime.UtcNow
            };

            ticket.Messages.Add(msg);
            ticket.UpdatedAt = DateTime.UtcNow;

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
        return await AddStaffMessageAsync(
            ticketId,
            systemAdminId,
            adminFullName,
            request.MessageContent,
            request.IsInternalNote ?? false,
            null,
            null,
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
            var normalizedStatus = NormalizeStatus(request.Status);

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

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null) return null;

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
            IEnumerable<AdminNotificationDto> query = state.AdminNotifications;

            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc == null);
            }

            var unreadCount = state.AdminNotifications.Count(n => n.ReadAtUtc == null);
            var items = query.Take(Math.Max(1, Math.Min(limit, 100))).ToList();

            return (items, unreadCount);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> GetAdminUnreadNotificationCountAsync(CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            return state.AdminNotifications.Count(n => n.ReadAtUtc == null);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<bool> MarkAdminNotificationReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var notif = state.AdminNotifications.FirstOrDefault(n => n.NotificationId == notificationId);
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

    public async Task<int> MarkAllAdminNotificationsReadAsync(CancellationToken cancellationToken = default)
    {
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var count = 0;
            foreach (var n in state.AdminNotifications.Where(n => n.ReadAtUtc == null))
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

            return ticketEntities.Select(e => MapEntityToDto(
                e,
                messages.Where(m => m.ticket_support_id == e.ticket_support_id).ToList(),
                attachments,
                assignments.Where(a => a.ticket_support_id == e.ticket_support_id).ToList(),
                departments,
                employees)).ToList();
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

            return MapEntityToDto(entity, messages, attachments, assignments, departments, employees);
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
        Dictionary<long, TblEmployee> employees)
    {
        var deptName = e.department_id.HasValue && departments.TryGetValue(e.department_id.Value, out var dn)
            ? dn : "KỸ THUẬT";

        var headAssignment = assignments.FirstOrDefault(a => a.ticket_support_employee_assignment_role == "DEPARTMENT_HEAD");
        var staffAssignment = assignments.FirstOrDefault(a => a.ticket_support_employee_assignment_role == "ASSIGNEE");

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
            DepartmentId = (int?)e.department_id,
            Department = deptName,
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
            DepartmentHeadEmployeeId = headEmp != null ? headEmp.EmployeeId : null,
            DepartmentHeadName = headEmp != null ? (headEmp.EmployeeFullName ?? headEmp.EmployeeAccount) : null,
            AssignedEmployeeId = staffEmp != null ? staffEmp.EmployeeId : null,
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
                return new SupportTicketMessageDto
                {
                    TicketMessageId = (int)m.ticket_message_id,
                    TicketSupportId = (int)(m.ticket_support_id ?? e.ticket_support_id),
                    SenderCustomerId = (int?)m.ticket_message_sender_customer_id,
                    SenderEmployeeId = (int?)m.ticket_message_sender_employee_id,
                    SenderName = m.ticket_message_sender_customer_id.HasValue
                        ? "Khách hàng"
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
            if (!string.IsNullOrWhiteSpace(t.TicketCode)) dict[t.TicketCode] = t;
        }
        return dict.Values.ToList();
    }

    private static bool BelongsToViewer(
        SupportTicketDto t,
        int tenantId,
        string tenantCode,
        int employeeId,
        bool isHead,
        int? employeeDeptId)
    {
        // 1. Nếu là người tạo ticket hoặc cùng tenant khách hàng
        if (t.CustomerId == employeeId || t.CreatedByEmployeeId == employeeId) return true;
        if (tenantId > 0 && t.TenantId == tenantId) return true;

        // 2. Nếu là Trưởng phòng ban: xem được tất cả ticket gửi đến phòng ban mình
        if (isHead && employeeDeptId.HasValue && t.DepartmentId == employeeDeptId.Value) return true;
        if (t.DepartmentHeadEmployeeId == employeeId) return true;

        // 3. Nếu là nhân viên được phân công xử lý ticket
        if (t.AssignedEmployeeId == employeeId) return true;
        if (t.Assignments.Any(a => a.EmployeeId == employeeId)) return true;

        return false;
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
            CreatedAt = m.CreatedAt
        }).ToList()
    };

    // ---------- Notifications ----------

    private async Task SendNewTicketNotificationAsync(SupportTicketDto ticket, string senderName, TblEmployee? head)
    {
        try
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (head != null && SmtpEmailOptions.IsEmailAddress(head.EmployeeEmail))
            {
                recipients.Add(head.EmployeeEmail!);
            }

            var adminRecipients = await ResolveSupportHandlerEmailsAsync();
            foreach (var r in adminRecipients) recipients.Add(r);

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            foreach (var recipient in recipients)
            {
                try
                {
                    var emailMsg = SupportTicketEmailTemplate.BuildNewTicketEmail(
                        recipient,
                        ticket,
                        senderName,
                        "1 tệp đính kèm",
                        systemAdminUrl);

                    await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gửi email thông báo ticket mới tới {Recipient} thất bại.", recipient);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xử lý gửi email thông báo ticket mới {Code}.", ticket.TicketCode);
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
                    <p>Yêu cầu hỗ trợ <strong>{ticket.TicketCode}</strong> của bạn đã được Trưởng phòng ban giải quyết:</p>
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
            var recipients = await ResolveSupportHandlerEmailsAsync();
            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            foreach (var recipient in recipients)
            {
                try
                {
                    var emailMsg = SupportTicketEmailTemplate.BuildCustomerReplyEmail(recipient, ticket, message, systemAdminUrl);
                    await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi email phản hồi tới {Recipient}.", recipient);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi email phản hồi ticket {Code}.", ticket.TicketCode);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveSupportHandlerEmailsAsync()
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    if (!string.Equals(trimmed, "Me@gmail.com", StringComparison.OrdinalIgnoreCase) && SmtpEmailOptions.IsEmailAddress(trimmed))
                    {
                        recipients.Add(trimmed);
                    }
                }
            }
        }
        catch { }

        var configured = _configuration["SupportTicket:HandlerEmail"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var part in configured.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (SmtpEmailOptions.IsEmailAddress(part)) recipients.Add(part);
            }
        }

        var smtp = _smtpOptions.Value;
        if (recipients.Count == 0)
        {
            if (SmtpEmailOptions.IsEmailAddress(smtp.FromAddress)) recipients.Add(smtp.FromAddress!);
            else if (SmtpEmailOptions.IsEmailAddress(smtp.Username)) recipients.Add(smtp.Username!);
        }

        return recipients.ToList();
    }

    private string BuildSystemAdminTicketUrl(int ticketSupportId)
    {
        var baseUrl = _configuration["SupportTicket:SystemAdminBaseUrl"]
            ?? _configuration["AccountRecovery:SystemAdminFrontendBaseUrl"]
            ?? "http://localhost:3001";

        return $"{baseUrl.TrimEnd('/')}/support-tickets/{ticketSupportId}";
    }
}
