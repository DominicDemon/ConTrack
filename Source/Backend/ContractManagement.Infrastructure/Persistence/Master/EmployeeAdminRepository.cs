using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>Accounts are placed by department + position: the department's staff or manager role is the employee's only internal role.</summary>
public sealed class EmployeeAdminRepository(MasterDbContext db) : IEmployeeAdmin
{
    private const string InternalScope = "INTERNAL";
    private static readonly PasswordHasher<Employee> Hasher = new();

    public async Task<List<EmployeeAdminRow>> Employees(CancellationToken ct)
    {
        var employees = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeDisplayName).ToListAsync(ct);
        var departments = await db.EmployeeDepartments.AsNoTracking().ToDictionaryAsync(d => d.EmployeeId, d => d.DepartmentId, ct);
        var managers = await ManagerIds(ct);
        return employees.Select(e => Row(e, departments.TryGetValue(e.EmployeeId, out var d) ? d : null, managers.Contains(e.EmployeeId))).ToList();
    }

    public async Task<List<DepartmentOption>> Departments(CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.DepartmentStatus == EmployeeAdminRules.Active && d.DepartmentManagerRoleId != null)
            .OrderBy(d => d.DepartmentId).ToListAsync(ct);
        var permissions = (await (from rp in db.SystemRolePermissions.AsNoTracking()
                join p in db.SystemPermissions on rp.SystemPermissionId equals p.SystemPermissionId
                select new { rp.SystemRoleId, p.SystemPermissionCode }).ToListAsync(ct))
            .GroupBy(x => x.SystemRoleId).ToDictionary(g => g.Key, g => g.Select(x => x.SystemPermissionCode).Order().ToArray());
        var managerIds = departments.Where(d => d.DepartmentManagerEmployeeId != null).Select(d => d.DepartmentManagerEmployeeId!.Value).ToList();
        var names = await db.Employees.AsNoTracking().Where(e => managerIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, e => e.EmployeeDisplayName, ct);
        return departments.Select(d => new DepartmentOption(d.DepartmentId, d.DepartmentCode, d.DepartmentName,
            d.DepartmentStaffRoleId is { } s ? permissions.GetValueOrDefault(s, []) : null,
            permissions.GetValueOrDefault(d.DepartmentManagerRoleId!.Value, []),
            d.DepartmentManagerEmployeeId, d.DepartmentManagerEmployeeId is { } m ? names.GetValueOrDefault(m) : null)).ToList();
    }

    public async Task<TemporaryPasswordResult> Create(CreateEmployeeInput input, InternalUser actor, CancellationToken ct)
    {
        var form = EmployeeAdminRules.Normalize(input);
        if (await db.Employees.AnyAsync(e => e.EmployeeUsername == form.Username, ct)) throw new InternalAppException(409, "Tên đăng nhập đã được dùng.");
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == form.Code, ct)) throw new InternalAppException(409, "Mã nhân viên đã được dùng.");
        if (form.Email is not null && await db.Employees.AnyAsync(e => e.EmployeeEmail == form.Email, ct)) throw new InternalAppException(409, "Email đã được dùng cho nhân viên khác.");
        if (form.Phone is not null && await db.Employees.AnyAsync(e => e.EmployeePhone == form.Phone, ct)) throw new InternalAppException(409, "Số điện thoại đã được dùng cho nhân viên khác.");
        var department = await Position(form.DepartmentId!.Value, form.Manager, null, ct);

        var now = DateTime.UtcNow;
        var password = EmployeeAdminRules.TemporaryPassword();
        var employee = new Employee { EmployeeCode = form.Code!, EmployeeUsername = form.Username!, EmployeeDisplayName = form.Name!, EmployeeEmail = form.Email,
            EmployeePhone = form.Phone, EmployeeStatus = EmployeeAdminRules.Active, EmployeeMustChangePassword = true, CreatedAt = now, CreatedBy = actor.Id };
        employee.EmployeePasswordHash = Hasher.HashPassword(employee, password);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Employees.Add(employee);
            await db.SaveChangesAsync(ct);
            await Place(employee.EmployeeId, department, form.Manager, actor.Id, now, ct);
            ReviewData.Audit(db, "CREATE", "EMPLOYEE", employee.EmployeeId, "EMPLOYEE", actor.Id, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return new(Row(employee, department.DepartmentId, form.Manager), password);
    }

    public async Task<EmployeeAdminRow> SetPosition(long id, EmployeePositionInput input, InternalUser actor, CancellationToken ct)
    {
        EmployeeAdminRules.RequireOther(actor, id);
        var department = await Position(EmployeeAdminRules.DepartmentId(input.DepartmentId), input.Manager, id, ct);
        var employee = await Find(id, ct);
        var now = DateTime.UtcNow;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Place(id, department, input.Manager, actor.Id, now, ct);
            employee.UpdatedAt = now; employee.UpdatedBy = actor.Id;
            ReviewData.Audit(db, "CHANGE_POSITION", "EMPLOYEE", id, "EMPLOYEE", actor.Id, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return Row(employee, department.DepartmentId, input.Manager);
    }

    public async Task<EmployeeAdminRow> SetStatus(long id, EmployeeStatusInput input, InternalUser actor, CancellationToken ct)
    {
        EmployeeAdminRules.RequireOther(actor, id);
        var employee = await Find(id, ct);
        var now = DateTime.UtcNow;
        employee.EmployeeStatus = input.Active ? EmployeeAdminRules.Active : EmployeeAdminRules.Locked;
        employee.UpdatedAt = now; employee.UpdatedBy = actor.Id;
        ReviewData.Audit(db, input.Active ? "UNLOCK" : "LOCK", "EMPLOYEE", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return await Row(employee, ct);
    }

    public async Task<TemporaryPasswordResult> ResetPassword(long id, InternalUser actor, CancellationToken ct)
    {
        EmployeeAdminRules.RequireOther(actor, id);
        var employee = await Find(id, ct);
        var now = DateTime.UtcNow;
        var password = EmployeeAdminRules.TemporaryPassword();
        employee.EmployeePasswordHash = Hasher.HashPassword(employee, password);
        employee.EmployeeMustChangePassword = true;
        employee.UpdatedAt = now; employee.UpdatedBy = actor.Id;
        ReviewData.Audit(db, "RESET_PASSWORD", "EMPLOYEE", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return new(await Row(employee, ct), password);
    }

    public async Task ChangePassword(long employeeId, EmployeeChangePasswordInput input, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.EmployeeId == employeeId && e.EmployeeStatus == EmployeeAdminRules.Active, ct)
            ?? throw new InternalAppException(401, "Vui lòng đăng nhập.");
        if (string.IsNullOrEmpty(input.CurrentPassword) || input.CurrentPassword.Length > 200
            || Hasher.VerifyHashedPassword(employee, employee.EmployeePasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new ArgumentException("Mật khẩu hiện tại không đúng.");
        if (!EmployeeAdminRules.IsValidPassword(input.NewPassword)) throw new ArgumentException(EmployeeAdminRules.PasswordMessage);
        if (input.NewPassword == input.CurrentPassword) throw new ArgumentException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        var now = DateTime.UtcNow;
        employee.EmployeePasswordHash = Hasher.HashPassword(employee, input.NewPassword!);
        employee.EmployeeMustChangePassword = false;
        employee.UpdatedAt = now; employee.UpdatedBy = employeeId;
        ReviewData.Audit(db, "CHANGE_PASSWORD", "EMPLOYEE", employeeId, "EMPLOYEE", employeeId, now);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Employee> Find(long id, CancellationToken ct) =>
        await db.Employees.SingleOrDefaultAsync(e => e.EmployeeId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy nhân viên.");

    /// <summary>Checks the department offers the position and, for a manager, that the seat is free (or already held by <paramref name="employeeId"/>).</summary>
    private async Task<Department> Position(long departmentId, bool manager, long? employeeId, CancellationToken ct)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.DepartmentId == departmentId && d.DepartmentStatus == EmployeeAdminRules.Active && d.DepartmentManagerRoleId != null, ct)
            ?? throw new ArgumentException("Phòng ban không tồn tại hoặc đã ngừng hoạt động.");
        if (!manager && department.DepartmentStaffRoleId is null) throw new ArgumentException($"{department.DepartmentName} chỉ có chức vụ quản lý.");
        if (manager && department.DepartmentManagerEmployeeId is { } current && current != employeeId)
        {
            var name = await db.Employees.Where(e => e.EmployeeId == current).Select(e => e.EmployeeDisplayName).FirstOrDefaultAsync(ct);
            throw new InternalAppException(409, $"{department.DepartmentName} đã có quản lý: {name}. Chuyển người đó sang chức vụ nhân viên hoặc phòng khác trước.");
        }
        return department;
    }

    /// <summary>Makes the employee a member of exactly this department, with its staff or manager role as the only internal role.</summary>
    private async Task Place(long employeeId, Department department, bool manager, long actorId, DateTime now, CancellationToken ct)
    {
        var held = await db.Departments.Where(d => d.DepartmentManagerEmployeeId == employeeId && (d.DepartmentId != department.DepartmentId || !manager)).ToListAsync(ct);
        foreach (var d in held) { d.DepartmentManagerEmployeeId = null; d.UpdatedAt = now; d.UpdatedBy = actorId; }
        // Free the old seat first: UX_Department_Manager would reject the new seat while the old one is still held.
        if (held.Count > 0 && manager) await db.SaveChangesAsync(ct);
        if (manager) { department.DepartmentManagerEmployeeId = employeeId; department.UpdatedAt = now; department.UpdatedBy = actorId; }

        var memberships = await db.EmployeeDepartments.Where(d => d.EmployeeId == employeeId).ToListAsync(ct);
        db.EmployeeDepartments.RemoveRange(memberships.Where(d => d.DepartmentId != department.DepartmentId));
        if (memberships.All(d => d.DepartmentId != department.DepartmentId))
            db.EmployeeDepartments.Add(new EmployeeDepartment { EmployeeId = employeeId, DepartmentId = department.DepartmentId, CreatedAt = now, CreatedBy = actorId });

        var roleId = manager ? department.DepartmentManagerRoleId!.Value : department.DepartmentStaffRoleId!.Value;
        // Roles outside the internal scope (other modules) are left untouched.
        var current = await (from er in db.EmployeeRoles join r in db.SystemRoles on er.SystemRoleId equals r.SystemRoleId
            where er.EmployeeId == employeeId && r.SystemRoleScope == InternalScope select er).ToListAsync(ct);
        db.EmployeeRoles.RemoveRange(current.Where(r => r.SystemRoleId != roleId));
        if (current.All(r => r.SystemRoleId != roleId))
            db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = employeeId, SystemRoleId = roleId, SystemMemberRoleAssignedAt = now, CreatedAt = now, CreatedBy = actorId });
    }

    private async Task<HashSet<long>> ManagerIds(CancellationToken ct) =>
        (await db.Departments.AsNoTracking().Where(d => d.DepartmentManagerEmployeeId != null).Select(d => d.DepartmentManagerEmployeeId!.Value).ToListAsync(ct)).ToHashSet();

    private async Task<EmployeeAdminRow> Row(Employee e, CancellationToken ct)
    {
        var department = await db.EmployeeDepartments.AsNoTracking().Where(d => d.EmployeeId == e.EmployeeId).Select(d => (long?)d.DepartmentId).FirstOrDefaultAsync(ct);
        return Row(e, department, (await ManagerIds(ct)).Contains(e.EmployeeId));
    }

    private static EmployeeAdminRow Row(Employee e, long? departmentId, bool manager) => new(e.EmployeeId, e.EmployeeCode, e.EmployeeUsername, e.EmployeeDisplayName,
        e.EmployeeEmail, e.EmployeePhone, e.EmployeeStatus, e.EmployeeMustChangePassword, e.EmployeeLastLoginAt, departmentId, manager);
}
