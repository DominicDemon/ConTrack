using System.Text.Json;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Email;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.Persistence.Master;
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

    Task<(IReadOnlyList<CustomerNotificationDto> Items, int UnreadCount)> GetCustomerNotificationsAsync(
        int customerId,
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> GetCustomerUnreadNotificationCountAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkCustomerNotificationReadAsync(
        int customerId,
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllCustomerNotificationsReadAsync(
        int customerId,
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
    public List<CustomerNotificationDto> CustomerNotifications { get; set; } = [];
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
    // Database first: TICKET_* tables come from the MASTER script; the app never creates them at runtime.

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
        // employeeId ở luồng khách là CUSTOMER.customer_id của người gửi; không có khách thì không ghi thay cho khách 1.
        if (employeeId <= 0) throw new ArgumentException("Thiếu khách hàng gửi yêu cầu hỗ trợ.");
        SupportTicketDto createdTicket;
        Employee? head = null;
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
            Department? dept = null;
            var ticketCode = $"TK-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            int? persistedTicketId = null;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<MasterDbContext>();
                if (db != null)
                {
                    if (string.IsNullOrWhiteSpace(tenantName) || string.IsNullOrWhiteSpace(tenantCode))
                    {
                        var customer = await db.Customers.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CustomerId == employeeId, cancellationToken);
                        if (customer != null)
                        {
                            if (string.IsNullOrWhiteSpace(tenantName))
                                tenantName = customer.CustomerDisplayName ?? senderName;
                            if (string.IsNullOrWhiteSpace(tenantCode))
                                tenantCode = customer.CustomerPersonalPhone ?? string.Empty;
                        }
                    }

                    if (resolvedServiceId.HasValue && resolvedServiceId.Value > 0 && string.IsNullOrWhiteSpace(resolvedServiceName))
                    {
                        var svc = await db.Services.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.ServiceId == resolvedServiceId.Value, cancellationToken);
                        if (svc != null && !string.IsNullOrWhiteSpace(svc.ServiceName))
                        {
                            resolvedServiceName = svc.ServiceName;
                        }
                    }
                    else if (!resolvedServiceId.HasValue && !string.IsNullOrWhiteSpace(resolvedServiceName))
                    {
                        var svc = await db.Services.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.ServiceName == resolvedServiceName, cancellationToken);
                        if (svc != null)
                        {
                            resolvedServiceId = svc.ServiceId;
                        }
                    }

                    dept = await db.Departments.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.DepartmentId == resolvedDeptId, cancellationToken);
                    if (dept != null && !string.IsNullOrWhiteSpace(dept.DepartmentName))
                    {
                        departmentDisplayName = dept.DepartmentName;
                    }

                    // NGHIỆP VỤ: Tìm đúng TRƯỞNG PHÒNG của phòng ban mà khách hàng gửi yêu cầu (KHÔNG gửi cho sysadmin hay phòng ban khác)
                    head = await FindDepartmentHeadAsync(db, resolvedDeptId, cancellationToken);

                    try
                    {
                        var entity = new TicketSupport
                        {
                            CustomerId = employeeId,
                            TenantId = tenantId > 0 ? tenantId : 1,
                            ProductId = request.ProductId,
                            ServiceId = resolvedServiceId,
                            TicketCode = ticketCode,
                            TicketTitle = request.ResolvedSubject,
                            TicketDescription = request.ResolvedDescription,
                            TicketPriority = NormalizePriority(request.ResolvedPriority),
                            TicketStatus = "Open",
                            CreatedAt = DateTime.UtcNow
                        };

                        db.TicketSupports.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);
                        persistedTicketId = (int)entity.TicketSupportId;

                        await TryUpdateTicketDepartmentIdAsync(db, entity.TicketSupportId, resolvedDeptId, cancellationToken);

                        // Lưu vào TICKET_SUPPORT_EMPLOYEE với role = DEPARTMENT_HEAD (chỉ Trưởng phòng của phòng ban này)
                        if (head != null)
                        {
                            var headAssignment = new TicketSupportEmployee
                            {
                                TicketSupportId = entity.TicketSupportId,
                                EmployeeId = head.EmployeeId,
                                TicketSupportEmployeeAssignmentRole = "DEPARTMENT_HEAD",
                                TicketSupportEmployeeAssignedAt = DateTime.UtcNow,
                                TicketSupportEmployeeAssignedByEmployeeId = null
                            };
                            db.TicketSupportEmployees.Add(headAssignment);
                        }

                        var initMsg = new TicketMessage
                        {
                            TicketSupportId = entity.TicketSupportId,
                            TicketMessageSenderCustomerId = employeeId,
                            TicketMessageContent = request.ResolvedDescription,
                            TicketMessageType = "INITIAL",
                            CreatedAt = DateTime.UtcNow
                        };
                        db.TicketMessages.Add(initMsg);
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

                            await TryInsertTicketAttachmentAsync(db, initMsg.TicketMessageId, safeDbUrl, safeDbName, cancellationToken);
                        }

                        await TryInsertTicketHistoryAsync(
                            db,
                            entity.TicketSupportId,
                            head != null ? (int)head.EmployeeId : null,
                            null,
                            null,
                            null,
                            "Open",
                            $"Khởi tạo phiếu hỗ trợ bởi {senderName} gửi tới {departmentDisplayName}.",
                            cancellationToken);

                        _logger.LogInformation("Đã lưu Ticket {TicketCode} cho phòng ban #{DeptId} ({DeptName}), Trưởng phòng nhận: {HeadName}.",
                            ticketCode, resolvedDeptId, departmentDisplayName, head?.EmployeeDisplayName ?? "Chưa xác định");
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
                CustomerId = employeeId,
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
                DepartmentHeadEmployeeId = head != null ? (int)head.EmployeeId : null,
                DepartmentHeadName = head != null ? (head.EmployeeDisplayName ?? head.EmployeeUsername) : null,
                ViewerRole = "CUSTOMER",
                AttachmentUrls = request.AttachmentUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [],
                AttachmentNames = request.AttachmentNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [],
                Assignments = head != null ? [
                    new TicketEmployeeAssignmentDto
                    {
                        TicketSupportEmployeeId = 1,
                        TicketSupportId = ticketId,
                        EmployeeId = (int)head.EmployeeId,
                        EmployeeName = head.EmployeeDisplayName ?? head.EmployeeUsername ?? "Trưởng phòng",
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
                        SenderCustomerId = employeeId,
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
                TargetEmployeeId = (int?)head?.EmployeeId,
                ActionKey = "new-ticket",
                CreatedAtUtc = DateTime.UtcNow
            };
            state.AdminNotifications.Insert(0, notification);

            await SaveStateAsync(state, cancellationToken);
            createdTicket = ticket;
        }
        finally
        {
            StoreLock.Release();
        }

        // Gửi email thông báo cho Trưởng phòng của phòng ban tiếp nhận (ngoài lock để không nghẽn xử lý khác)
        await SendNewTicketNotificationAsync(createdTicket, senderName, head);
        return CloneTicketWithViewer(createdTicket, "CUSTOMER", employeeId, null);
    }

    public async Task<IReadOnlyList<DepartmentEmployeeDto>> GetDepartmentStaffAsync(
        int ticketId,
        int currentEmployeeId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<MasterDbContext>();
        if (db == null) return [];

        var deptMap = await LoadTicketDepartmentIdsAsync(db, [ticketId], cancellationToken);
        int? deptId = deptMap.TryGetValue(ticketId, out var dbDeptId) ? dbDeptId : null;
        if (!deptId.HasValue)
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

        var dept = await db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == deptId.Value, cancellationToken);
        var deptName = dept?.DepartmentName ?? GetDepartmentDisplayName(deptId.Value, null);
        var deptManagerId = dept?.DepartmentManagerEmployeeId;

        var allActiveEmployees = await db.Employees.AsNoTracking()
            .Where(e => e.EmployeeStatus == "ACTIVE")
            .OrderBy(e => e.EmployeeDisplayName)
            .ToListAsync(cancellationToken);

        var empDeptMap = await LoadEmployeeDepartmentMapAsync(db, cancellationToken);

        // Trả về Trưởng phòng của phòng ban (isManager = true) và các Nhân viên (STAFF) thuộc đúng phòng ban đó
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

            var isManager = deptManagerId.HasValue && deptManagerId.Value == emp.EmployeeId;

            filteredStaff.Add(new DepartmentEmployeeDto
            {
                EmployeeId = (int)emp.EmployeeId,
                EmployeeName = CleanMojibake(!string.IsNullOrWhiteSpace(emp.EmployeeDisplayName) ? emp.EmployeeDisplayName : emp.EmployeeUsername) ?? string.Empty,
                EmployeeCode = emp.EmployeeCode,
                EmployeeEmail = emp.EmployeeEmail,
                DepartmentId = deptId.Value,
                DepartmentName = CleanMojibake(deptName) ?? deptName,
                IsManager = isManager
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
        SupportTicketDto? assignedTicket = null;
        var staffToNotify = new List<Employee>();
        string? assignmentNote = request.Note;
        string roleForViewer = "STAFF";
        int? deptForViewer = null;

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            var isHeadOrAdmin = string.Equals(roleInfo.Role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleInfo.Role, "SYSADMIN", StringComparison.OrdinalIgnoreCase);
            if (!isHeadOrAdmin)
            {
                throw new ArgumentException("Chỉ Trưởng phòng mới có quyền phân công xử lý yêu cầu hỗ trợ.");
            }
            roleForViewer = roleInfo.Role;
            deptForViewer = roleInfo.DepartmentId;

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var existingStateTicket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (existingStateTicket != null && string.Equals(NormalizeStatus(existingStateTicket.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể phân công tiếp.");
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<MasterDbContext>();

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

            var targetStaffIds = new List<int>();
            if (request.StaffEmployeeIds is { Count: > 0 })
            {
                targetStaffIds.AddRange(request.StaffEmployeeIds.Where(id => id > 0));
            }
            if (request.StaffEmployeeId > 0 && !targetStaffIds.Contains(request.StaffEmployeeId))
            {
                targetStaffIds.Add(request.StaffEmployeeId);
            }
            targetStaffIds = targetStaffIds.Distinct().ToList();
            if (targetStaffIds.Count == 0)
            {
                targetStaffIds.Add(currentEmployeeId);
            }

            var isSelfAssignOnly = targetStaffIds.Count == 1 && targetStaffIds[0] == currentEmployeeId;
            var now = DateTime.UtcNow;
            var selectedStaffEntities = new List<Employee>();
            var hadPreviousAssignee = false;

            if (db != null)
            {
                var longIds = targetStaffIds.Select(x => (long)x).ToList();
                selectedStaffEntities = await db.Employees.AsNoTracking()
                    .Where(e => longIds.Contains(e.EmployeeId))
                    .ToListAsync(cancellationToken);

                var ticketEntity = await db.TicketSupports.FirstOrDefaultAsync(t => t.TicketSupportId == ticketId, cancellationToken);

                if (ticketEntity != null && selectedStaffEntities.Count > 0)
                {
                    if (string.Equals(NormalizeStatus(ticketEntity.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể phân công tiếp.");
                    }

                    var oldStatus = ticketEntity.TicketStatus ?? "Open";
                    ticketEntity.TicketStatus = "In_Progress";
                    if (!string.IsNullOrWhiteSpace(assignedPriority))
                    {
                        ticketEntity.TicketPriority = assignedPriority;
                    }
                    ticketEntity.UpdatedAt = now;

                    var activeDbAssignees = await db.TicketSupportEmployees
                        .Where(a => a.TicketSupportId == ticketId
                                    && a.TicketSupportEmployeeAssignmentRole == "ASSIGNEE"
                                    && a.TicketSupportEmployeeUnassignedAt == null)
                        .ToListAsync(cancellationToken);

                    if (activeDbAssignees.Count > 0)
                    {
                        hadPreviousAssignee = true;
                        foreach (var oldAssignee in activeDbAssignees)
                        {
                            if (!oldAssignee.EmployeeId.HasValue || !targetStaffIds.Contains((int)oldAssignee.EmployeeId.Value))
                            {
                                oldAssignee.TicketSupportEmployeeUnassignedAt = now;
                            }
                        }
                    }

                    foreach (var st in selectedStaffEntities)
                    {
                        var alreadyActive = activeDbAssignees.Any(a => a.EmployeeId == st.EmployeeId && a.TicketSupportEmployeeUnassignedAt == null);
                        if (!alreadyActive)
                        {
                            db.TicketSupportEmployees.Add(new TicketSupportEmployee
                            {
                                TicketSupportId = ticketId,
                                EmployeeId = st.EmployeeId,
                                TicketSupportEmployeeAssignmentRole = "ASSIGNEE",
                                TicketSupportEmployeeAssignedAt = now,
                                TicketSupportEmployeeAssignedByEmployeeId = currentEmployeeId
                            });
                        }
                    }

                    var staffNamesText = string.Join(", ", selectedStaffEntities.Select(s => s.EmployeeDisplayName ?? s.EmployeeUsername ?? $"NV #{s.EmployeeId}"));
                    var noteText = string.IsNullOrWhiteSpace(request.Note)
                        ? (isSelfAssignOnly
                            ? "Trưởng phòng trực tiếp tiếp nhận và xử lý yêu cầu."
                            : (hadPreviousAssignee
                                ? "Điều phối chuyển giao/bổ sung nhân sự tiếp nhận xử lý yêu cầu."
                                : "Vui lòng tiếp nhận và xử lý yêu cầu của khách hàng."))
                        : request.Note.Trim();

                    var actionPrefix = isSelfAssignOnly
                        ? "Trưởng phòng tự xử lý"
                        : (hadPreviousAssignee ? "Điều phối / Chuyển giao xử lý" : "Phân công xử lý");

                    var historyNotes = isSelfAssignOnly
                        ? $"Trưởng phòng {currentEmployeeName} trực tiếp tiếp nhận xử lý yêu cầu (Độ ưu tiên: {priorityLabel}). Ghi chú: {noteText}"
                        : $"Trưởng phòng {currentEmployeeName} {(hadPreviousAssignee ? "điều phối lại / giao" : "phân công")} cho {staffNamesText} (Độ ưu tiên: {priorityLabel}). Nội dung chỉ đạo: {noteText}";

                    var msg = new TicketMessage
                    {
                        TicketSupportId = ticketId,
                        TicketMessageSenderEmployeeId = currentEmployeeId,
                        TicketMessageType = "ASSIGNMENT",
                        TicketMessageContent = isSelfAssignOnly
                            ? $"[{actionPrefix} • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã trực tiếp tiếp nhận xử lý yêu cầu.\nGhi chú: {noteText}"
                            : $"[{actionPrefix} • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã giao yêu cầu cho: {staffNamesText}.\nNội dung chỉ đạo: {noteText}",
                        CreatedAt = now
                    };
                    db.TicketMessages.Add(msg);

                    await db.SaveChangesAsync(cancellationToken);
                    await TryInsertTicketHistoryAsync(
                        db,
                        ticketId,
                        currentEmployeeId,
                        targetStaffIds.FirstOrDefault(),
                        currentEmployeeId,
                        oldStatus,
                        "In_Progress",
                        historyNotes,
                        cancellationToken);
                }
            }

            var ticket = existingStateTicket;
            if (ticket != null)
            {
                var staffDict = selectedStaffEntities.ToDictionary(s => (int)s.EmployeeId);
                var staffNamesList = targetStaffIds.Select(id =>
                    staffDict.TryGetValue(id, out var emp)
                        ? (emp.EmployeeDisplayName ?? emp.EmployeeUsername ?? $"Nhân viên #{id}")
                        : (id == currentEmployeeId ? currentEmployeeName : $"Nhân viên #{id}")).ToList();
                var joinedNames = string.Join(", ", staffNamesList);

                var activeStateAssignees = ticket.Assignments
                    .Where(a => string.Equals(a.AssignmentRole, "ASSIGNEE", StringComparison.OrdinalIgnoreCase) && a.UnassignedAt == null)
                    .ToList();

                if (activeStateAssignees.Count > 0)
                {
                    hadPreviousAssignee = true;
                    foreach (var oldAssign in activeStateAssignees)
                    {
                        if (!targetStaffIds.Contains(oldAssign.EmployeeId))
                        {
                            oldAssign.UnassignedAt = now;
                        }
                    }
                }

                var noteText = string.IsNullOrWhiteSpace(request.Note)
                    ? (isSelfAssignOnly
                        ? "Trưởng phòng trực tiếp tiếp nhận và xử lý yêu cầu."
                        : (hadPreviousAssignee
                            ? "Điều phối chuyển giao/bổ sung nhân sự tiếp nhận xử lý yêu cầu."
                            : "Vui lòng tiếp nhận và xử lý yêu cầu của khách hàng."))
                    : request.Note.Trim();

                var actionPrefix = isSelfAssignOnly
                    ? "Trưởng phòng tự xử lý"
                    : (hadPreviousAssignee ? "Điều phối / Chuyển giao xử lý" : "Phân công xử lý");

                ticket.TicketStatus = "In_Progress";
                if (!string.IsNullOrWhiteSpace(assignedPriority))
                {
                    ticket.TicketPriority = assignedPriority;
                }
                ticket.AssignedEmployeeId = targetStaffIds.FirstOrDefault();
                ticket.AssignedEmployeeIds = [.. targetStaffIds];
                ticket.AssignedEmployeeName = joinedNames;
                ticket.AssignedAt = now;
                ticket.UpdatedAt = now;

                foreach (var targetId in targetStaffIds)
                {
                    var alreadyActive = ticket.Assignments.Any(a =>
                        string.Equals(a.AssignmentRole, "ASSIGNEE", StringComparison.OrdinalIgnoreCase)
                        && a.EmployeeId == targetId
                        && a.UnassignedAt == null);

                    if (!alreadyActive)
                    {
                        var emp = staffDict.GetValueOrDefault(targetId);
                        var empName = emp?.EmployeeDisplayName ?? emp?.EmployeeUsername ?? (targetId == currentEmployeeId ? currentEmployeeName : $"Nhân viên #{targetId}");
                        ticket.Assignments.Add(new TicketEmployeeAssignmentDto
                        {
                            TicketSupportEmployeeId = ticket.Assignments.Count + 1,
                            TicketSupportId = ticketId,
                            EmployeeId = targetId,
                            EmployeeName = empName,
                            EmployeeCode = emp?.EmployeeCode,
                            EmployeeEmail = emp?.EmployeeEmail,
                            AssignmentRole = "ASSIGNEE",
                            AssignedAt = now,
                            UnassignedAt = null,
                            AssignedByEmployeeId = currentEmployeeId,
                            AssignedByName = currentEmployeeName
                        });
                    }
                }

                ticket.Messages.Add(new SupportTicketMessageDto
                {
                    TicketMessageId = state.NextMessageId++,
                    TicketSupportId = ticketId,
                    SenderEmployeeId = currentEmployeeId,
                    SenderName = currentEmployeeName,
                    IsFromEmployee = true,
                    TicketMessageContent = isSelfAssignOnly
                        ? $"[{actionPrefix} • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã trực tiếp tiếp nhận xử lý yêu cầu.\nGhi chú: {noteText}"
                        : $"[{actionPrefix} • Ưu tiên: {priorityLabel}] Trưởng phòng {currentEmployeeName} đã giao yêu cầu cho: {joinedNames}.\nNội dung chỉ đạo: {noteText}",
                    TicketMessageType = "ASSIGNMENT",
                    CreatedAt = now
                });

                state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
                state.Tickets.Insert(0, ticket);

                foreach (var targetId in targetStaffIds.Where(id => id != currentEmployeeId))
                {
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
                        TargetEmployeeId = targetId,
                        ActionKey = "assigned-ticket",
                        CreatedAtUtc = now
                    });

                    if (staffDict.TryGetValue(targetId, out var empToEmail))
                    {
                        staffToNotify.Add(empToEmail);
                    }
                }

                await SaveStateAsync(state, cancellationToken);
                assignedTicket = ticket;
            }
        }
        finally
        {
            StoreLock.Release();
        }

        if (assignedTicket != null && staffToNotify.Count > 0)
        {
            foreach (var staffMember in staffToNotify)
            {
                await SendStaffAssignmentNotificationAsync(assignedTicket, staffMember, currentEmployeeName, assignmentNote);
            }
        }

        return assignedTicket is null ? null : CloneTicketWithViewer(assignedTicket, roleForViewer, currentEmployeeId, deptForViewer);
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

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket is null) return null;

            if (!CanEmployeeViewTicket(ticket, currentEmployeeId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }

            if (string.Equals(NormalizeStatus(ticket.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể gửi thêm báo cáo.");
            }

            // Nếu là nhân viên (STAFF), kiểm tra xem nhân viên này có đang là người phụ trách hiện tại không
            if (string.Equals(roleInfo.Role, "STAFF", StringComparison.OrdinalIgnoreCase))
            {
                var isActiveAssignee = ticket.AssignedEmployeeId == currentEmployeeId
                    || ticket.AssignedEmployeeIds.Contains(currentEmployeeId)
                    || ticket.Assignments.Any(a =>
                        a.EmployeeId == currentEmployeeId
                        && string.Equals(a.AssignmentRole, "ASSIGNEE", StringComparison.OrdinalIgnoreCase)
                        && a.UnassignedAt == null);

                if (!isActiveAssignee)
                {
                    throw new ArgumentException("Yêu cầu này đã được chuyển giao cho nhân sự khác phụ trách. Bạn chỉ có thể xem lịch sử.");
                }
            }

            var isUnresolved = string.Equals(request.ReportType, "UNRESOLVED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.ReportType, "HANDOVER", StringComparison.OrdinalIgnoreCase);
            var msgType = isUnresolved ? "STAFF_REPORT" : "STAFF_COMPLETION";
            var msgContent = isUnresolved
                ? $"[Báo cáo chưa xử lý được / Đề xuất chuyển người] Nhân viên {currentEmployeeName} báo cáo lên Trưởng phòng:\n{request.Resolution.Trim()}"
                : $"[Báo cáo hoàn tất xử lý] Nhân viên {currentEmployeeName} đã xử lý xong và gửi kết quả lên Trưởng phòng:\n{request.Resolution.Trim()}";
            var newStatus = "PendingHeadApproval";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<MasterDbContext>();

            if (db != null)
            {
                var ticketEntity = await db.TicketSupports.FirstOrDefaultAsync(t => t.TicketSupportId == ticketId, cancellationToken);
                if (ticketEntity != null)
                {
                    if (!isUnresolved)
                    {
                        ticketEntity.TicketResolution = request.Resolution.Trim();
                    }
                    ticketEntity.TicketStatus = newStatus;
                    ticketEntity.UpdatedAt = DateTime.UtcNow;

                    var msg = new TicketMessage
                    {
                        TicketSupportId = ticketId,
                        TicketMessageSenderEmployeeId = currentEmployeeId,
                        TicketMessageType = msgType,
                        TicketMessageContent = msgContent,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.TicketMessages.Add(msg);

                    await db.SaveChangesAsync(cancellationToken);
                    await TryInsertTicketHistoryAsync(
                        db,
                        ticketId,
                        currentEmployeeId,
                        null,
                        currentEmployeeId,
                        "In_Progress",
                        newStatus,
                        isUnresolved
                            ? $"Nhân viên {currentEmployeeName} báo cáo chưa xử lý được / đề xuất chuyển người: {request.Resolution.Trim()}"
                            : $"Nhân viên {currentEmployeeName} gửi kết quả hoàn tất lên Trưởng phòng: {request.Resolution.Trim()}",
                        cancellationToken);
                }
            }

            if (!isUnresolved)
            {
                ticket.TicketResolution = request.Resolution.Trim();
            }
            ticket.TicketStatus = newStatus;
            ticket.UpdatedAt = DateTime.UtcNow;

            ticket.Messages.Add(new SupportTicketMessageDto
            {
                TicketMessageId = state.NextMessageId++,
                TicketSupportId = ticketId,
                SenderEmployeeId = currentEmployeeId,
                SenderName = currentEmployeeName,
                IsFromEmployee = true,
                TicketMessageContent = msgContent,
                TicketMessageType = msgType,
                CreatedAt = DateTime.UtcNow
            });

            state.Tickets.RemoveAll(t => t.TicketSupportId == ticketId);
            state.Tickets.Insert(0, ticket);

            state.AdminNotifications.Insert(0, new AdminNotificationDto
            {
                NotificationId = state.NextNotificationId++,
                SubjectType = "SupportTicket",
                SubjectId = ticket.TicketSupportId,
                TicketCode = ticket.TicketCode,
                TenantName = ticket.TenantName,
                TenantCode = ticket.TenantCode,
                Title = isUnresolved
                    ? $"[{ticket.Department}] NV {currentEmployeeName} đề xuất phân công lại {ticket.TicketCode}"
                    : $"[{ticket.Department}] NV {currentEmployeeName} báo cáo hoàn tất {ticket.TicketCode}",
                Body = isUnresolved
                    ? $"Nhân viên {currentEmployeeName} báo chưa xử lý được ticket '{ticket.TicketSubject}', cần Trưởng phòng điều phối người khác."
                    : $"Nhân viên {currentEmployeeName} đã gửi kết quả xử lý ticket '{ticket.TicketSubject}' lên Trưởng phòng.",
                Priority = ticket.TicketPriority,
                DepartmentId = ticket.DepartmentId,
                Department = ticket.Department,
                TargetEmployeeId = ticket.DepartmentHeadEmployeeId,
                ActionKey = isUnresolved ? "staff-unresolved-ticket" : "staff-completed-ticket",
                CreatedAtUtc = DateTime.UtcNow
            });

            await SaveStateAsync(state, cancellationToken);

            return CloneTicketWithViewer(ticket, roleInfo.Role, currentEmployeeId, roleInfo.DepartmentId);
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
        SupportTicketDto? resolvedTicket = null;
        string? noticeToEmail = null;
        string roleForViewer = "STAFF";
        int? deptForViewer = null;

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(currentEmployeeId, cancellationToken);
            var isHeadOrAdmin = string.Equals(roleInfo.Role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleInfo.Role, "SYSADMIN", StringComparison.OrdinalIgnoreCase);
            if (!isHeadOrAdmin)
            {
                throw new ArgumentException("Chỉ Trưởng phòng mới được phép duyệt kết quả và phản hồi hoàn tất cho khách hàng.");
            }
            roleForViewer = roleInfo.Role;
            deptForViewer = roleInfo.DepartmentId;

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var existingTicket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (existingTicket != null && string.Equals(NormalizeStatus(existingTicket.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng trước đó.");
            }

            var roleLabel = "Trưởng phòng";
            var targetStatus = request.CloseTicket ? "Resolved" : "In_Progress";
            var resolutionText = request.Resolution;
            var customerNotice = !string.IsNullOrWhiteSpace(request.MessageToCustomer)
                ? request.MessageToCustomer
                : $"[Kết quả xử lý từ {roleLabel} {currentEmployeeName}]:\n{resolutionText}";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<MasterDbContext>();

            if (db != null)
            {
                var ticketEntity = await db.TicketSupports.FirstOrDefaultAsync(t => t.TicketSupportId == ticketId, cancellationToken);
                if (ticketEntity != null)
                {
                    if (string.Equals(NormalizeStatus(ticketEntity.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng trước đó.");
                    }

                    var oldStatus = ticketEntity.TicketStatus ?? "In_Progress";
                    ticketEntity.TicketResolution = resolutionText;
                    ticketEntity.TicketStatus = targetStatus;
                    ticketEntity.TicketResolvedAt = DateTime.UtcNow;
                    if (request.CloseTicket) ticketEntity.TicketClosedAt = DateTime.UtcNow;
                    ticketEntity.UpdatedAt = DateTime.UtcNow;

                    var msg = new TicketMessage
                    {
                        TicketSupportId = ticketId,
                        TicketMessageSenderEmployeeId = currentEmployeeId,
                        TicketMessageType = "RESOLUTION",
                        TicketMessageContent = customerNotice,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.TicketMessages.Add(msg);

                    await db.SaveChangesAsync(cancellationToken);
                    await TryInsertTicketHistoryAsync(
                        db,
                        ticketId,
                        currentEmployeeId,
                        null,
                        currentEmployeeId,
                        oldStatus,
                        targetStatus,
                        $"{roleLabel} {currentEmployeeName} gửi kết quả cho khách hàng: {customerNotice}",
                        cancellationToken);
                }
            }

            var ticket = existingTicket;
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
                    ticket.AssignedEmployeeIds = [currentEmployeeId];
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

                var notifTitle = request.CloseTicket
                    ? $"Có kết quả xử lý yêu cầu [{ticket.TicketCode}]"
                    : $"Trưởng phòng cập nhật kết quả [{ticket.TicketCode}]";
                var notifBody = $"{currentEmployeeName} ({ticket.Department}): {TruncatePreview(customerNotice, 160)}";
                AddCustomerNotification(state, ticket, notifTitle, notifBody, request.CloseTicket ? "ticket-resolved" : "ticket-updated");

                await SaveStateAsync(state, cancellationToken);
                resolvedTicket = ticket;
                noticeToEmail = customerNotice;
            }
        }
        finally
        {
            StoreLock.Release();
        }

        if (resolvedTicket != null && !string.IsNullOrWhiteSpace(noticeToEmail))
        {
            await SendResolutionNoticeToCustomerAsync(resolvedTicket, noticeToEmail, currentEmployeeName, request.CloseTicket);
        }

        return resolvedTicket is null ? null : CloneTicketWithViewer(resolvedTicket, roleForViewer, currentEmployeeId, deptForViewer);
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
        // employeeId ở luồng khách là CUSTOMER.customer_id của người gửi; không có khách thì không ghi thay cho khách 1.
        if (employeeId <= 0) throw new ArgumentException("Thiếu khách hàng gửi tin nhắn.");
        SupportTicketDto? updatedTicket = null;
        SupportTicketMessageDto? createdMsg = null;

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket is null) return null;

            if (string.Equals(NormalizeStatus(ticket.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể gửi thêm tin nhắn.");
            }

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
                var db = scope.ServiceProvider.GetService<MasterDbContext>();
                if (db != null)
                {
                    try
                    {
                        var entity = new TicketMessage
                        {
                            TicketSupportId = ticketId,
                            TicketMessageSenderCustomerId = employeeId,
                            TicketMessageContent = content,
                            TicketMessageType = "CUSTOMER_REPLY",
                            CreatedAt = DateTime.UtcNow
                        };
                        db.TicketMessages.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(firstUrl) || !string.IsNullOrWhiteSpace(firstName))
                        {
                            var safeDbUrl = firstUrl != null && firstUrl.Length > 950
                                ? (firstName ?? "image-attachment.png")
                                : (firstUrl ?? firstName);
                            var safeDbName = firstName != null && firstName.Length > 950
                                ? firstName[..950]
                                : firstName;

                            await TryInsertTicketAttachmentAsync(db, entity.TicketMessageId, safeDbUrl, safeDbName, cancellationToken);
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

            // Chỉ tạo thông báo tin nhắn khách hàng cho Trưởng phòng (vì chỉ Trưởng phòng mới nhắn tin với khách hàng)
            var targetEmpId = ticket.DepartmentHeadEmployeeId ?? ticket.AssignedEmployeeId;
            state.AdminNotifications.Insert(0, new AdminNotificationDto
            {
                NotificationId = state.NextNotificationId++,
                SubjectType = "SupportTicket",
                SubjectId = ticket.TicketSupportId,
                TicketCode = ticket.TicketCode,
                TenantName = ticket.TenantName,
                TenantCode = ticket.TenantCode,
                Title = $"[{ticket.Department}] Khách hàng nhắn tin {ticket.TicketCode}",
                Body = $"{senderName}: {content}",
                Priority = ticket.TicketPriority,
                DepartmentId = ticket.DepartmentId,
                Department = ticket.Department,
                TargetEmployeeId = targetEmpId,
                ActionKey = "customer-replied-ticket",
                CreatedAtUtc = DateTime.UtcNow
            });

            await SaveStateAsync(state, cancellationToken);
            updatedTicket = ticket;
            createdMsg = msg;
        }
        finally
        {
            StoreLock.Release();
        }

        if (updatedTicket != null && createdMsg != null)
        {
            await SendCustomerReplyEmailsSafeAsync(updatedTicket, createdMsg);
        }

        return createdMsg;
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
        SupportTicketDto? updatedTicket = null;
        SupportTicketMessageDto? createdMsg = null;

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
            var isHeadOrAdmin = string.Equals(roleInfo.Role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleInfo.Role, "SYSADMIN", StringComparison.OrdinalIgnoreCase);
            if (!isHeadOrAdmin)
            {
                throw new ArgumentException("Chỉ Trưởng phòng mới được phép nhắn tin trao đổi với khách hàng.");
            }

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId)
                ?? await LoadSingleTicketFromDbAsync(ticketId, cancellationToken);
            if (ticket is null) return null;

            if (!CanEmployeeViewTicket(ticket, employeeId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }

            if (string.Equals(NormalizeStatus(ticket.TicketStatus), "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể gửi thêm tin nhắn.");
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<MasterDbContext>();
                if (db != null)
                {
                    try
                    {
                        var entity = new TicketMessage
                        {
                            TicketSupportId = ticketId,
                            TicketMessageSenderEmployeeId = employeeId,
                            TicketMessageContent = normalizedContent,
                            TicketMessageType = isInternalNote ? "INTERNAL_NOTE" : "STAFF_REPLY",
                            CreatedAt = DateTime.UtcNow
                        };
                        db.TicketMessages.Add(entity);
                        await db.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(attachmentUrl) || !string.IsNullOrWhiteSpace(attachmentName))
                        {
                            var safeDbUrl = attachmentUrl != null && attachmentUrl.Length > 950
                                ? (attachmentName ?? "image-attachment.png")
                                : (attachmentUrl ?? attachmentName);
                            var safeDbName = attachmentName != null && attachmentName.Length > 950
                                ? attachmentName[..950]
                                : attachmentName;

                            await TryInsertTicketAttachmentAsync(db, entity.TicketMessageId, safeDbUrl, safeDbName, cancellationToken);
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

            if (!isInternalNote)
            {
                AddCustomerNotification(
                    state,
                    ticket,
                    $"Phản hồi mới cho yêu cầu [{ticket.TicketCode}]",
                    $"{employeeName} ({ticket.Department}): {TruncatePreview(normalizedContent, 160)}",
                    "admin-replied-ticket");
            }

            await SaveStateAsync(state, cancellationToken);
            updatedTicket = ticket;
            createdMsg = msg;
        }
        finally
        {
            StoreLock.Release();
        }

        if (!isInternalNote && updatedTicket != null && createdMsg != null)
        {
            await SendResolutionNoticeToCustomerAsync(updatedTicket, createdMsg.TicketMessageContent, employeeName, isClosed: false);
        }

        return createdMsg;
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
        SupportTicketDto? updatedTicket = null;
        bool shouldNotifyCustomer = false;
        string? resolutionNotice = null;
        string roleForViewer = "STAFF";
        int? deptForViewer = null;

        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var roleInfo = await ResolveEmployeeRoleInfoAsync(systemAdminId, cancellationToken);
            roleForViewer = roleInfo.Role;
            deptForViewer = roleInfo.DepartmentId;

            var normalizedStatus = NormalizeStatus(request.Status);

            var state = await EnsureStateLoadedAsync(cancellationToken);
            var ticket = state.Tickets.FirstOrDefault(t => t.TicketSupportId == ticketId);
            if (ticket is null) return null;

            if (!CanEmployeeViewTicket(ticket, systemAdminId, roleInfo.Role, roleInfo.DepartmentId))
            {
                return null;
            }
            var isHeadOrAdmin = string.Equals(roleInfo.Role, "DEPARTMENT_HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleInfo.Role, "SYSADMIN", StringComparison.OrdinalIgnoreCase);
            if (!isHeadOrAdmin)
            {
                throw new ArgumentException("Chỉ Trưởng phòng mới có quyền cập nhật trạng thái phiếu hỗ trợ.");
            }
            var wasResolved = string.Equals(ticket.TicketStatus, "Resolved", StringComparison.OrdinalIgnoreCase);
            if (wasResolved)
            {
                throw new ArgumentException("Yêu cầu hỗ trợ này đã hoàn thành và đóng, không thể thay đổi trạng thái.");
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<MasterDbContext>();
                if (db != null)
                {
                    var entity = await db.TicketSupports.FirstOrDefaultAsync(t => t.TicketSupportId == ticketId, cancellationToken);
                    if (entity != null)
                    {
                        var oldStatus = entity.TicketStatus;
                        entity.TicketStatus = normalizedStatus;
                        if (!string.IsNullOrWhiteSpace(request.Resolution)) entity.TicketResolution = request.Resolution;
                        if (normalizedStatus == "Resolved")
                        {
                            entity.TicketResolvedAt = DateTime.UtcNow;
                            entity.TicketClosedAt = DateTime.UtcNow;
                        }
                        entity.UpdatedAt = DateTime.UtcNow;

                        await db.SaveChangesAsync(cancellationToken);
                        await TryInsertTicketHistoryAsync(
                            db,
                            ticketId,
                            null,
                            null,
                            systemAdminId > 0 ? systemAdminId : null,
                            oldStatus,
                            normalizedStatus,
                            request.Resolution,
                            cancellationToken);
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
                if (!wasResolved)
                {
                    var notice = string.IsNullOrWhiteSpace(request.Resolution)
                        ? "Yêu cầu hỗ trợ của bạn đã được xử lý hoàn tất."
                        : request.Resolution.Trim();
                    AddCustomerNotification(
                        state,
                        ticket,
                        $"Có kết quả xử lý yêu cầu [{ticket.TicketCode}]",
                        $"{adminFullName} ({ticket.Department}): {TruncatePreview(notice, 160)}",
                        "ticket-resolved");
                    shouldNotifyCustomer = true;
                    resolutionNotice = notice;
                }
            }
            ticket.UpdatedAt = DateTime.UtcNow;

            await SaveStateAsync(state, cancellationToken);
            updatedTicket = ticket;
        }
        finally
        {
            StoreLock.Release();
        }

        if (shouldNotifyCustomer && updatedTicket != null && !string.IsNullOrWhiteSpace(resolutionNotice))
        {
            await SendResolutionNoticeToCustomerAsync(updatedTicket, resolutionNotice, adminFullName, isClosed: true);
        }

        return updatedTicket is null ? null : CloneTicketWithViewer(updatedTicket, roleForViewer, systemAdminId, deptForViewer);
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

    public async Task<(IReadOnlyList<CustomerNotificationDto> Items, int UnreadCount)> GetCustomerNotificationsAsync(
        int customerId,
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return ([], 0);
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var scopedList = state.CustomerNotifications
                .Where(n => n.CustomerId == customerId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .ToList();

            IEnumerable<CustomerNotificationDto> query = scopedList;
            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc == null);
            }
            else if (string.Equals(status, "read", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.ReadAtUtc != null);
            }

            var unreadCount = scopedList.Count(n => n.ReadAtUtc == null);
            var items = query.Take(Math.Clamp(limit, 1, 100)).ToList();
            return (items, unreadCount);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<int> GetCustomerUnreadNotificationCountAsync(int customerId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return 0;
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            return state.CustomerNotifications.Count(n => n.CustomerId == customerId && n.ReadAtUtc == null);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    public async Task<bool> MarkCustomerNotificationReadAsync(int customerId, int notificationId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return false;
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var notif = state.CustomerNotifications.FirstOrDefault(n => n.CustomerId == customerId && n.NotificationId == notificationId);
            if (notif is null) return false;

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

    public async Task<int> MarkAllCustomerNotificationsReadAsync(int customerId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return 0;
        await StoreLock.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureStateLoadedAsync(cancellationToken);
            var count = 0;
            foreach (var n in state.CustomerNotifications.Where(n => n.CustomerId == customerId && n.ReadAtUtc == null))
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

    private static void AddCustomerNotification(
        SupportTicketStoreState state,
        SupportTicketDto ticket,
        string title,
        string body,
        string actionKey)
    {
        var customerId = ticket.CustomerId ?? ticket.CreatedByEmployeeId ?? 0;
        if (customerId <= 0) return;

        state.CustomerNotifications.Insert(0, new CustomerNotificationDto
        {
            NotificationId = state.NextNotificationId++,
            CustomerId = customerId,
            SubjectType = "SupportTicket",
            SubjectId = ticket.TicketSupportId,
            TicketCode = ticket.TicketCode,
            Department = ticket.Department,
            Priority = ticket.TicketPriority,
            Title = title,
            Body = body,
            ActionKey = actionKey,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static string TruncatePreview(string text, int maxLength)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
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
            var db = scope.ServiceProvider.GetService<MasterDbContext>();
            if (db == null)
            {
                return ("SYSADMIN", null, null);
            }


            var emp = await db.Employees
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
        if (employeeId <= 0)
        {
            return false;
        }

        if (role == "SYSADMIN")
        {
            return true;
        }

        if (role == "DEPARTMENT_HEAD")
        {
            var ticketDeptId = t.DepartmentId ?? ResolveDepartmentIdFromInput(null, t.Department);
            if (employeeDeptId.HasValue && ticketDeptId == employeeDeptId.Value) return true;
            if (t.DepartmentHeadEmployeeId == employeeId) return true;
            if (t.AssignedEmployeeId == employeeId || t.AssignedEmployeeIds.Contains(employeeId)) return true;
            return false;
        }

        if (role == "STAFF")
        {
            if (t.AssignedEmployeeId == employeeId || t.AssignedEmployeeIds.Contains(employeeId)) return true;
            return t.Assignments.Any(a =>
                a.EmployeeId == employeeId
                && string.Equals(a.AssignmentRole, "ASSIGNEE", StringComparison.OrdinalIgnoreCase));
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
            return t.CustomerId == customerId;
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
            var db = scope.ServiceProvider.GetService<MasterDbContext>();
            if (db == null) return defaultServices;

            var dbServices = await db.Services
                .AsNoTracking()
                .Where(s => s.ServiceStatus == 1)
                .OrderBy(s => s.ServiceId)
                .Select(s => new SupportServiceItemDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName ?? $"Dịch vụ #{s.ServiceId}",
                    Description = s.ServiceMetadescription ?? s.ServiceUnitname
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

    private sealed record TicketAttachmentRow(long TicketMessageId, string? FileName1, string? FileName2);

    private static async Task WithConnectionAsync(MasterDbContext db, Func<System.Data.Common.DbConnection, Task> action, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(ct);
        try
        {
            await action(conn);
        }
        finally
        {
            if (shouldClose) await conn.CloseAsync();
        }
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    /// <summary>Phòng ban và trưởng phòng lấy từ dữ liệu (005-department-roles.sql): EMPLOYEE_DEPARTMENT + DEPARTMENT.department_manager_employee_id / department_manager_role_id.</summary>
    private static async Task<Dictionary<int, (int DepartmentId, bool IsHead)>> LoadEmployeeDepartmentMapAsync(MasterDbContext db, CancellationToken ct)
    {
        var map = new Dictionary<int, (int DepartmentId, bool IsHead)>();
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT CAST(ed.[employee_id] AS INT), CAST(ed.[department_id] AS INT), "
                    + "CAST(CASE WHEN d.[department_manager_employee_id] = ed.[employee_id] "
                    + "OR EXISTS (SELECT 1 FROM [EMPLOYEE_ROLE] er WHERE er.[employee_id] = ed.[employee_id] AND er.[system_role_id] = d.[department_manager_role_id]) "
                    + "THEN 1 ELSE 0 END AS BIT) "
                    + "FROM [EMPLOYEE_DEPARTMENT] ed JOIN [DEPARTMENT] d ON d.[department_id] = ed.[department_id]";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    map[reader.GetInt32(0)] = (reader.GetInt32(1), reader.GetBoolean(2));
                }
            }, ct);
        }
        catch
        {
            // Không đọc được phòng ban: nhân viên được coi là chưa xếp phòng (ClassifyEmployee).
        }
        return map;
    }

    private static async Task TryUpdateTicketDepartmentIdAsync(MasterDbContext db, long ticketId, int departmentId, CancellationToken ct)
    {
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "IF COL_LENGTH('dbo.TICKET_SUPPORT', 'department_id') IS NOT NULL UPDATE [TICKET_SUPPORT] SET [department_id] = @deptId WHERE [ticket_support_id] = @id";
                AddParam(cmd, "@deptId", departmentId);
                AddParam(cmd, "@id", ticketId);
                await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }
        catch { }
    }

    private static async Task<Dictionary<long, int>> LoadTicketDepartmentIdsAsync(MasterDbContext db, IReadOnlyList<long> ticketIds, CancellationToken ct)
    {
        var map = new Dictionary<long, int>();
        if (ticketIds.Count == 0) return map;
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                var idList = string.Join(",", ticketIds);
                cmd.CommandText = $"IF COL_LENGTH('dbo.TICKET_SUPPORT', 'department_id') IS NOT NULL SELECT [ticket_support_id], CAST([department_id] AS INT) FROM [TICKET_SUPPORT] WHERE [department_id] IS NOT NULL AND [ticket_support_id] IN ({idList})";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    map[reader.GetInt64(0)] = reader.GetInt32(1);
                }
            }, ct);
        }
        catch { }
        return map;
    }

    private static async Task TryInsertTicketAttachmentAsync(MasterDbContext db, long messageId, string? fileName1, string? fileName2, CancellationToken ct)
    {
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "IF OBJECT_ID('dbo.TICKET_ATTACHMENTS', 'U') IS NOT NULL INSERT INTO [TICKET_ATTACHMENTS] ([ticket_message_id],[file_name_1],[file_name_2],[mime_type],[uploaded_at]) VALUES (@msgId,@f1,@f2,'image/png',SYSUTCDATETIME())";
                AddParam(cmd, "@msgId", messageId);
                AddParam(cmd, "@f1", fileName1);
                AddParam(cmd, "@f2", fileName2);
                await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }
        catch { }
    }

    private static async Task<List<TicketAttachmentRow>> LoadTicketAttachmentsAsync(MasterDbContext db, IReadOnlyList<long> messageIds, CancellationToken ct)
    {
        var list = new List<TicketAttachmentRow>();
        if (messageIds.Count == 0) return list;
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                var idList = string.Join(",", messageIds);
                cmd.CommandText = $"IF OBJECT_ID('dbo.TICKET_ATTACHMENTS', 'U') IS NOT NULL SELECT [ticket_message_id], [file_name_1], [file_name_2] FROM [TICKET_ATTACHMENTS] WHERE [ticket_message_id] IN ({idList})";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    list.Add(new TicketAttachmentRow(
                        reader.GetInt64(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2)));
                }
            }, ct);
        }
        catch { }
        return list;
    }

    private static async Task TryInsertTicketHistoryAsync(
        MasterDbContext db,
        long ticketId,
        int? employeeStart,
        int? employeeFinished,
        int? changedByUserId,
        string? oldStatus,
        string? newStatus,
        string? notes,
        CancellationToken ct)
    {
        try
        {
            await WithConnectionAsync(db, async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "IF OBJECT_ID('dbo.TICKET_HISTORY', 'U') IS NOT NULL INSERT INTO [TICKET_HISTORY] ([ticket_support_id],[employee_id_start],[employee_id_finished],[changed_by_user_id],[old_status],[new_status],[notes],[changed_at]) VALUES (@tid,@s,@f,@u,@old,@new,@notes,SYSUTCDATETIME())";
                AddParam(cmd, "@tid", ticketId);
                AddParam(cmd, "@s", employeeStart);
                AddParam(cmd, "@f", employeeFinished);
                AddParam(cmd, "@u", changedByUserId);
                AddParam(cmd, "@old", oldStatus);
                AddParam(cmd, "@new", newStatus);
                AddParam(cmd, "@notes", notes);
                await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }
        catch { }
    }

    private static (string Role, int? DepartmentId) ClassifyEmployee(Employee emp, Dictionary<int, (int DepartmentId, bool IsHead)> empDeptMap)
    {
        if (emp.EmployeeCode == "ADM-SYSADMIN" || emp.EmployeeUsername == "sysadmin")
        {
            return ("SYSADMIN", null);
        }

        // Chưa xếp phòng ban: không thuộc phòng nào, chỉ thấy ticket được giao đích danh.
        if (!empDeptMap.TryGetValue((int)emp.EmployeeId, out var membership))
        {
            var fallbackIsHead = emp.EmployeeUsername.StartsWith("head.");
            return (fallbackIsHead ? "DEPARTMENT_HEAD" : "STAFF", null);
        }

        // Ban Giám Đốc (phòng 1)
        if (membership.DepartmentId == 1)
        {
            return ("SYSADMIN", null);
        }

        var isHead = membership.IsHead || emp.EmployeeUsername.StartsWith("head.");
        return (isHead ? "DEPARTMENT_HEAD" : "STAFF", membership.DepartmentId);
    }

    private static async Task<Employee?> FindDepartmentHeadAsync(MasterDbContext db, int departmentId, CancellationToken ct)
    {
        var allEmps = await db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeStatus == "ACTIVE")
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
            var db = scope.ServiceProvider.GetService<MasterDbContext>();
            if (db == null) return [];

            var ticketEntities = await db.TicketSupports
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .ToListAsync(ct);

            return ticketEntities.Count == 0
                ? []
                : await HydrateTicketDtosAsync(db, ticketEntities, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể đọc tickets từ MasterDbContext, sử dụng file cache.");
            return [];
        }
    }

    private async Task<SupportTicketDto?> LoadSingleTicketFromDbAsync(int ticketId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<MasterDbContext>();
            if (db == null) return null;

            var entity = await db.TicketSupports
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketSupportId == ticketId, ct);

            if (entity == null) return null;

            var list = await HydrateTicketDtosAsync(db, [entity], ct);
            return list.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không thể đọc ticket {Id} từ MasterDbContext.", ticketId);
            return null;
        }
    }

    private static async Task<List<SupportTicketDto>> HydrateTicketDtosAsync(
        MasterDbContext db,
        IReadOnlyList<TicketSupport> ticketEntities,
        CancellationToken ct)
    {
        var ticketIds = ticketEntities.Select(t => t.TicketSupportId).ToList();
        var deptIdMap = await LoadTicketDepartmentIdsAsync(db, ticketIds, ct);

        var messages = await db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketSupportId.HasValue && ticketIds.Contains(m.TicketSupportId.Value))
            .ToListAsync(ct);

        var messageIds = messages.Select(m => m.TicketMessageId).ToList();
        var attachments = await LoadTicketAttachmentsAsync(db, messageIds, ct);

        var assignments = await db.TicketSupportEmployees
            .AsNoTracking()
            .Where(a => a.TicketSupportId.HasValue && ticketIds.Contains(a.TicketSupportId.Value))
            .ToListAsync(ct);

        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.DepartmentId, d => d.DepartmentName, ct);
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.EmployeeId, ct);
        var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.ServiceId, s => s.ServiceName ?? string.Empty, ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.CustomerId, ct);
        var empDeptMap = await LoadEmployeeDepartmentMapAsync(db, ct);

        return ticketEntities.Select(e =>
        {
            int? resolvedDeptId = deptIdMap.TryGetValue(e.TicketSupportId, out var dId) ? dId : null;
            var ticketAssignments = assignments.Where(a => a.TicketSupportId == e.TicketSupportId).ToList();
            if (!resolvedDeptId.HasValue)
            {
                var headEmpId = ticketAssignments.FirstOrDefault(a => a.EmployeeId.HasValue)?.EmployeeId;
                if (headEmpId.HasValue && empDeptMap.TryGetValue((int)headEmpId.Value, out var membership))
                {
                    resolvedDeptId = membership.DepartmentId;
                }
            }
            return MapEntityToDto(
                e,
                resolvedDeptId,
                messages.Where(m => m.TicketSupportId == e.TicketSupportId).ToList(),
                attachments,
                ticketAssignments,
                departments,
                employees,
                services,
                customers);
        }).ToList();
    }

    private static SupportTicketDto MapEntityToDto(
        TicketSupport e,
        int? departmentId,
        IReadOnlyList<TicketMessage> messages,
        IReadOnlyList<TicketAttachmentRow> attachments,
        IReadOnlyList<TicketSupportEmployee> assignments,
        Dictionary<long, string> departments,
        Dictionary<long, Employee> employees,
        Dictionary<long, string>? services = null,
        Dictionary<long, Customer>? customers = null)
    {
        var resolvedDeptId = departmentId ?? 2;
        var deptName = departments.TryGetValue(resolvedDeptId, out var dn)
            ? dn : GetDepartmentDisplayName(resolvedDeptId, null);

        string? serviceName = null;
        if (e.ServiceId.HasValue && services != null && services.TryGetValue(e.ServiceId.Value, out var sn) && !string.IsNullOrWhiteSpace(sn))
        {
            serviceName = sn;
        }

        Customer? cust = e.CustomerId.HasValue && customers != null && customers.TryGetValue(e.CustomerId.Value, out var c) ? c : null;
        var customerDisplayName = cust?.CustomerDisplayName ?? cust?.CustomerEmail ?? "Khách hàng";
        var customerCompany = cust?.CustomerDisplayName ?? string.Empty;
        var customerCode = cust?.CustomerPersonalPhone ?? string.Empty;

        var headAssignment = assignments
            .Where(a => a.TicketSupportEmployeeAssignmentRole == "DEPARTMENT_HEAD" && a.TicketSupportEmployeeUnassignedAt == null)
            .OrderByDescending(a => a.TicketSupportEmployeeAssignedAt)
            .FirstOrDefault()
            ?? assignments.FirstOrDefault(a => a.TicketSupportEmployeeAssignmentRole == "DEPARTMENT_HEAD");

        var activeStaffAssignments = assignments
            .Where(a => a.TicketSupportEmployeeAssignmentRole == "ASSIGNEE" && a.TicketSupportEmployeeUnassignedAt == null)
            .OrderBy(a => a.TicketSupportEmployeeAssignedAt)
            .ToList();

        var staffAssignment = activeStaffAssignments.LastOrDefault();

        Employee? headEmp = headAssignment?.EmployeeId != null && employees.TryGetValue(headAssignment.EmployeeId.Value, out var h) ? h : null;
        Employee? staffEmp = staffAssignment?.EmployeeId != null && employees.TryGetValue(staffAssignment.EmployeeId.Value, out var s) ? s : null;

        var activeStaffIds = activeStaffAssignments
            .Where(a => a.EmployeeId.HasValue)
            .Select(a => (int)a.EmployeeId!.Value)
            .Distinct()
            .ToList();

        var activeStaffNames = activeStaffIds
            .Select(id => employees.TryGetValue(id, out var emp)
                ? (emp.EmployeeDisplayName ?? emp.EmployeeUsername ?? $"NV #{id}")
                : $"NV #{id}")
            .ToList();

        var attachmentDict = attachments
            .GroupBy(a => a.TicketMessageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new SupportTicketDto
        {
            TicketSupportId = (int)e.TicketSupportId,
            CustomerId = (int?)e.CustomerId,
            TenantId = (int?)e.TenantId,
            TenantName = customerCompany,
            TenantCode = customerCode,
            CreatedByEmployeeId = (int?)e.CustomerId,
            CreatedByName = customerDisplayName,
            DepartmentId = resolvedDeptId,
            Department = deptName,
            ServiceId = e.ServiceId,
            ServiceName = serviceName,
            Module = serviceName ?? "Dịch vụ chung",
            TicketCode = e.TicketCode ?? $"TK-{e.TicketSupportId}",
            TicketSubject = e.TicketTitle ?? string.Empty,
            TicketDescription = e.TicketDescription ?? string.Empty,
            TicketPriority = NormalizePriority(e.TicketPriority),
            TicketStatus = NormalizeStatus(e.TicketStatus),
            TicketResolution = e.TicketResolution,
            TicketResolvedAt = e.TicketResolvedAt,
            TicketClosedAt = e.TicketClosedAt,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            DepartmentHeadEmployeeId = headEmp != null ? (int)headEmp.EmployeeId : (int?)headAssignment?.EmployeeId,
            DepartmentHeadName = headEmp != null ? (headEmp.EmployeeDisplayName ?? headEmp.EmployeeUsername) : null,
            AssignedEmployeeId = activeStaffIds.Count > 0 ? activeStaffIds[0] : (staffEmp != null ? (int)staffEmp.EmployeeId : (int?)staffAssignment?.EmployeeId),
            AssignedEmployeeIds = activeStaffIds,
            AssignedEmployeeName = activeStaffNames.Count > 0
                ? string.Join(", ", activeStaffNames)
                : (staffEmp != null ? (staffEmp.EmployeeDisplayName ?? staffEmp.EmployeeUsername) : null),
            AssignedAt = staffAssignment?.TicketSupportEmployeeAssignedAt,
            Assignments = assignments.Select(a =>
            {
                var emp = a.EmployeeId.HasValue ? employees.GetValueOrDefault(a.EmployeeId.Value) : null;
                var assigner = a.TicketSupportEmployeeAssignedByEmployeeId.HasValue
                    ? employees.GetValueOrDefault(a.TicketSupportEmployeeAssignedByEmployeeId.Value) : null;
                return new TicketEmployeeAssignmentDto
                {
                    TicketSupportEmployeeId = a.TicketSupportEmployeeId,
                    TicketSupportId = a.TicketSupportId ?? e.TicketSupportId,
                    EmployeeId = a.EmployeeId.HasValue ? (int)a.EmployeeId.Value : 0,
                    EmployeeName = emp?.EmployeeDisplayName ?? emp?.EmployeeUsername ?? $"NV #{a.EmployeeId}",
                    EmployeeCode = emp?.EmployeeCode,
                    EmployeeEmail = emp?.EmployeeEmail,
                    AssignmentRole = a.TicketSupportEmployeeAssignmentRole ?? string.Empty,
                    AssignedAt = a.TicketSupportEmployeeAssignedAt,
                    UnassignedAt = a.TicketSupportEmployeeUnassignedAt,
                    AssignedByEmployeeId = (int?)a.TicketSupportEmployeeAssignedByEmployeeId,
                    AssignedByName = assigner?.EmployeeDisplayName ?? assigner?.EmployeeUsername
                };
            }).ToList(),
            Messages = messages.OrderBy(m => m.CreatedAt).Select(m =>
            {
                var msgAtts = attachmentDict.GetValueOrDefault(m.TicketMessageId);
                var firstAtt = msgAtts?.FirstOrDefault();
                var senderCust = m.TicketMessageSenderCustomerId.HasValue && customers != null && customers.TryGetValue(m.TicketMessageSenderCustomerId.Value, out var sc) ? sc : cust;
                return new SupportTicketMessageDto
                {
                    TicketMessageId = (int)m.TicketMessageId,
                    TicketSupportId = (int)(m.TicketSupportId ?? e.TicketSupportId),
                    SenderCustomerId = (int?)m.TicketMessageSenderCustomerId,
                    SenderEmployeeId = (int?)m.TicketMessageSenderEmployeeId,
                    SenderName = m.TicketMessageSenderCustomerId.HasValue
                        ? (senderCust?.CustomerDisplayName ?? "Khách hàng")
                        : (m.TicketMessageSenderEmployeeId.HasValue && employees.TryGetValue(m.TicketMessageSenderEmployeeId.Value, out var se)
                            ? (se.EmployeeDisplayName ?? se.EmployeeUsername ?? "Nhân viên hỗ trợ")
                            : "Hệ thống hỗ trợ"),
                    IsFromEmployee = m.TicketMessageSenderEmployeeId.HasValue,
                    TicketMessageContent = m.TicketMessageContent ?? string.Empty,
                    TicketMessageType = m.TicketMessageType ?? "TEXT",
                    TicketMessageAttachmentUrl = firstAtt?.FileName1,
                    TicketMessageAttachmentName = firstAtt?.FileName2,
                    CreatedAt = m.CreatedAt ?? DateTime.UtcNow
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
                    if (t.AssignedEmployeeIds.Count == 0 && existing.AssignedEmployeeIds.Count > 0)
                        t.AssignedEmployeeIds = [.. existing.AssignedEmployeeIds];
                    if (existing.Assignments.Count > t.Assignments.Count)
                        t.Assignments = [.. existing.Assignments];

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
                    SanitizeStoreState(loaded);
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

    private static void SanitizeStoreState(SupportTicketStoreState state)
    {
        foreach (var t in state.Tickets)
        {
            t.DepartmentHeadName = CleanMojibake(t.DepartmentHeadName);
            t.AssignedEmployeeName = CleanMojibake(t.AssignedEmployeeName);
            t.Department = CleanMojibake(t.Department) ?? t.Department;
            t.TicketSubject = CleanMojibake(t.TicketSubject) ?? t.TicketSubject;
            t.TicketDescription = CleanMojibake(t.TicketDescription) ?? t.TicketDescription;
            t.CreatedByName = CleanMojibake(t.CreatedByName);
            t.TenantName = CleanMojibake(t.TenantName) ?? t.TenantName;
            foreach (var a in t.Assignments)
            {
                a.EmployeeName = CleanMojibake(a.EmployeeName) ?? a.EmployeeName;
                a.AssignedByName = CleanMojibake(a.AssignedByName);
            }
            foreach (var m in t.Messages)
            {
                m.SenderName = CleanMojibake(m.SenderName) ?? m.SenderName;
                m.TicketMessageContent = CleanMojibake(m.TicketMessageContent) ?? m.TicketMessageContent;
            }
        }
        foreach (var n in state.AdminNotifications)
        {
            n.Title = CleanMojibake(n.Title) ?? string.Empty;
            n.Body = CleanMojibake(n.Body) ?? string.Empty;
            n.Department = CleanMojibake(n.Department) ?? n.Department;
        }
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

        if (string.Equals(viewerRole, "STAFF", StringComparison.OrdinalIgnoreCase))
        {
            // Nhân viên được phân công không có phần nhắn tin với khách hàng: chỉ thấy INITIAL, ASSIGNMENT, STAFF_COMPLETION, STAFF_REPORT, RESOLUTION
            copy.Messages = copy.Messages
                .Where(m => !string.Equals(m.TicketMessageType, "CUSTOMER_REPLY", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(m.TicketMessageType, "STAFF_REPLY", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else if (string.Equals(viewerRole, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
        {
            // Khách hàng chỉ thấy tin nhắn trao đổi trực tiếp với Trưởng phòng & kết quả cuối cùng, không thấy trao đổi nội bộ
            copy.Messages = copy.Messages
                .Where(m => !string.Equals(m.TicketMessageType, "ASSIGNMENT", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(m.TicketMessageType, "STAFF_COMPLETION", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(m.TicketMessageType, "STAFF_REPORT", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(m.TicketMessageType, "INTERNAL_NOTE", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return copy;
    }

    private static string? CleanMojibake(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text
            .Replace("HoÃ ng TrÆ°á»Ÿng PhÃ²ng Kinh Doanh", "Hoàng Trưởng Phòng Kinh Doanh")
            .Replace("HoÃ ng TrÆ°á»Ÿng PhÃ²ng Kinh Doanh", "Hoàng Trưởng Phòng Kinh Doanh")
            .Replace("Nháº­t bá»‘ cá»§a cÃ¡c loáº¡i bá»‘", "Nhật Bố")
            .Replace("Nh\u00E1\u00BA\u00ADt b\u00E1\u00BB\u2018 c\u00C3\u00A1c lo\u00E1\u00BA\u00A1i b\u00E1\u00BB\u2018", "Nhật Bố")
            .Replace("Nhật bố của các loại bố", "Nhật Bố")
            .Replace("Ä áº·ng TrÆ°á»Ÿng PhÃ²ng Káº¿ ToÃ¡n", "Đặng Trưởng Phòng Kế Toán")
            .Replace("VÅ© TrÆ°á»Ÿng PhÃ²ng HÃ nh ChÃ­nh - PhÃ¡p Cháº¿", "Vũ Trưởng Phòng Hành Chính - Pháp Chế")
            .Replace("BÃ¹i ChuyÃªn ViÃªn PhÃ¡p Cháº¿", "Bùi Chuyên Viên Pháp Chế");
    }

    private static SupportTicketDto CloneTicket(SupportTicketDto src) => new()
    {
        TicketSupportId = src.TicketSupportId,
        CustomerId = src.CustomerId,
        TenantId = src.TenantId,
        TenantName = CleanMojibake(src.TenantName) ?? string.Empty,
        TenantCode = src.TenantCode,
        CreatedByEmployeeId = src.CreatedByEmployeeId,
        CreatedByName = CleanMojibake(src.CreatedByName),
        TicketCode = src.TicketCode,
        TicketSubject = CleanMojibake(src.TicketSubject) ?? string.Empty,
        TicketDescription = CleanMojibake(src.TicketDescription) ?? string.Empty,
        TicketPriority = src.TicketPriority,
        TicketStatus = src.TicketStatus,
        DepartmentId = src.DepartmentId,
        Department = CleanMojibake(src.Department) ?? string.Empty,
        ServiceId = src.ServiceId,
        ServiceName = CleanMojibake(src.ServiceName),
        Module = CleanMojibake(src.Module),
        TicketResolution = CleanMojibake(src.TicketResolution),
        TicketResolvedAt = src.TicketResolvedAt,
        TicketClosedAt = src.TicketClosedAt,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        DepartmentHeadEmployeeId = src.DepartmentHeadEmployeeId,
        DepartmentHeadName = CleanMojibake(src.DepartmentHeadName),
        AssignedEmployeeId = src.AssignedEmployeeId,
        AssignedEmployeeIds = [.. src.AssignedEmployeeIds],
        AssignedEmployeeName = CleanMojibake(src.AssignedEmployeeName),
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
            EmployeeName = CleanMojibake(a.EmployeeName) ?? string.Empty,
            EmployeeCode = a.EmployeeCode,
            EmployeeEmail = a.EmployeeEmail,
            AssignmentRole = a.AssignmentRole,
            AssignedAt = a.AssignedAt,
            UnassignedAt = a.UnassignedAt,
            AssignedByEmployeeId = a.AssignedByEmployeeId,
            AssignedByName = CleanMojibake(a.AssignedByName)
        }).ToList(),
        Messages = src.Messages.Select(m => new SupportTicketMessageDto
        {
            TicketMessageId = m.TicketMessageId,
            TicketSupportId = m.TicketSupportId,
            SenderCustomerId = m.SenderCustomerId,
            SenderEmployeeId = m.SenderEmployeeId,
            SenderName = CleanMojibake(m.SenderName) ?? string.Empty,
            IsFromEmployee = m.IsFromEmployee,
            TicketMessageContent = CleanMojibake(m.TicketMessageContent) ?? string.Empty,
            TicketMessageType = m.TicketMessageType,
            TicketMessageAttachmentUrl = m.TicketMessageAttachmentUrl,
            TicketMessageAttachmentName = CleanMojibake(m.TicketMessageAttachmentName),
            AttachmentUrls = [.. m.AttachmentUrls],
            AttachmentNames = [.. m.AttachmentNames],
            CreatedAt = m.CreatedAt
        }).ToList()
    };

    // ---------- Notifications (Strict Department Head & Assigned Staff Only; Never SysAdmin) ----------

    private async Task SendNewTicketNotificationAsync(SupportTicketDto ticket, string senderName, Employee? head)
    {
        try
        {
            // Chỉ gửi thông báo cho Trưởng phòng của phòng ban được khách hàng chọn (fallback HandlerEmail nếu trưởng phòng chưa có email hợp lệ)
            var recipient = head != null && SmtpEmailOptions.IsEmailAddress(head.EmployeeEmail)
                ? head.EmployeeEmail!
                : _configuration["SupportTicket:HandlerEmail"];

            if (!SmtpEmailOptions.IsEmailAddress(recipient))
            {
                _logger.LogWarning("Bỏ qua gửi email ticket mới {Code}: Trưởng phòng (#{HeadId}) chưa có email hợp lệ.", ticket.TicketCode, head?.EmployeeId);
                return;
            }

            var attachmentSummary = ticket.AttachmentNames.Count > 0
                ? string.Join(", ", ticket.AttachmentNames)
                : "Không có tệp đính kèm";
            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildNewTicketEmail(
                recipient!,
                ticket,
                senderName,
                attachmentSummary,
                systemAdminUrl);

            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
            _logger.LogInformation("Đã gửi email thông báo ticket mới {Code} tới Trưởng phòng ({Recipient}).", ticket.TicketCode, recipient);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi email thông báo ticket mới {Code} tới Trưởng phòng.", ticket.TicketCode);
        }
    }

    private async Task SendStaffAssignmentNotificationAsync(SupportTicketDto ticket, Employee staff, string headName, string? note)
    {
        try
        {
            if (!SmtpEmailOptions.IsEmailAddress(staff.EmployeeEmail))
            {
                _logger.LogWarning("Bỏ qua gửi email phân công ticket {Code}: Nhân viên #{EmpId} chưa có email hợp lệ.", ticket.TicketCode, staff.EmployeeId);
                return;
            }

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildStaffAssignmentEmail(
                staff.EmployeeEmail!,
                ticket,
                staff.EmployeeDisplayName ?? staff.EmployeeUsername ?? "Nhân viên",
                headName,
                note,
                systemAdminUrl);

            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
            _logger.LogInformation("Đã gửi email phân công ticket {Code} tới nhân viên #{EmpId} ({Recipient}).", ticket.TicketCode, staff.EmployeeId, staff.EmployeeEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi email phân công ticket {Code} cho nhân viên {EmpId}.", ticket.TicketCode, staff.EmployeeId);
        }
    }

    private async Task SendResolutionNoticeToCustomerAsync(SupportTicketDto ticket, string resolutionNotice, string senderName, bool isClosed)
    {
        try
        {
            string? customerEmail = null;
            if (ticket.CustomerId is { } customerId && customerId > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<MasterDbContext>();
                if (db != null)
                {
                    customerEmail = await db.Customers.AsNoTracking()
                        .Where(c => c.CustomerId == customerId)
                        .Select(c => c.CustomerEmail)
                        .FirstOrDefaultAsync();
                }
            }
            if (!SmtpEmailOptions.IsEmailAddress(customerEmail))
            {
                customerEmail = _configuration["SupportTicket:CustomerNotificationEmail"];
            }
            if (!SmtpEmailOptions.IsEmailAddress(customerEmail))
            {
                _logger.LogWarning("Bỏ qua gửi email kết quả ticket {Code} cho khách hàng #{CustomerId}: chưa có email hợp lệ.", ticket.TicketCode, ticket.CustomerId);
                return;
            }

            var portalUrl = BuildCustomerPortalTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildResolutionNoticeEmail(
                customerEmail!,
                ticket,
                resolutionNotice,
                senderName,
                portalUrl,
                isClosed);
            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
            _logger.LogInformation("Đã gửi email kết quả/phản hồi ticket {Code} tới khách hàng ({Recipient}).", ticket.TicketCode, customerEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi email thông báo kết quả ticket {Code} cho khách hàng.", ticket.TicketCode);
        }
    }

    private async Task SendCustomerReplyEmailsSafeAsync(SupportTicketDto ticket, SupportTicketMessageDto message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<MasterDbContext>();
            if (db == null) return;

            // Chỉ gửi email cho nhân viên đang phụ trách ticket hoặc Trưởng phòng của phòng ban đó (không gửi cho sysadmin)
            var targetEmpId = ticket.AssignedEmployeeId ?? ticket.DepartmentHeadEmployeeId;
            if (!targetEmpId.HasValue) return;

            var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == targetEmpId.Value);
            if (emp?.EmployeeEmail == null || !SmtpEmailOptions.IsEmailAddress(emp.EmployeeEmail)) return;

            var systemAdminUrl = BuildSystemAdminTicketUrl(ticket.TicketSupportId);
            var emailMsg = SupportTicketEmailTemplate.BuildCustomerReplyEmail(emp.EmployeeEmail, ticket, message, systemAdminUrl);
            await _emailTransport.SendAsync(emailMsg, CancellationToken.None);
            _logger.LogInformation("Đã gửi email phản hồi từ khách hàng cho ticket {Code} tới {Recipient}.", ticket.TicketCode, emp.EmployeeEmail);
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

        // 3001 now serves tickets inside the internal workspace; the old /support-tickets page used the legacy API.
        return $"{baseUrl.TrimEnd('/')}/workspace/support-tickets/{ticketSupportId}";
    }

    private string BuildCustomerPortalTicketUrl(int ticketSupportId)
    {
        var baseUrl = _configuration["SupportTicket:CustomerPortalBaseUrl"]
            ?? _configuration["AccountRecovery:TenantFrontendBaseUrl"]
            ?? "http://localhost:3000";

        return $"{baseUrl.TrimEnd('/')}/portal/support/{ticketSupportId}";
    }
}
