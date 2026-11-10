using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class EmployeeAdminRepository(MasterDbContext db) : IEmployeeAdmin
{
    private const string InternalScope = "INTERNAL";
    private static readonly PasswordHasher<Employee> Hasher = new();

    public async Task<List<EmployeeAdminRow>> Employees(CancellationToken ct)
    {
        var employees = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeDisplayName).ToListAsync(ct);
        var roles = await RoleIdsByEmployee(employees.Select(e => e.EmployeeId).ToList(), ct);
        return employees.Select(e => Row(e, roles.GetValueOrDefault(e.EmployeeId, []))).ToList();
    }

    public async Task<List<RoleOption>> Roles(CancellationToken ct)
    {
        var rows = await (from r in db.SystemRoles.AsNoTracking()
            where r.SystemRoleScope == InternalScope && r.SystemRoleStatus == EmployeeAdminRules.Active
            join rp in db.SystemRolePermissions on r.SystemRoleId equals rp.SystemRoleId into rps
            from rp in rps.DefaultIfEmpty()
            join p in db.SystemPermissions on rp.SystemPermissionId equals p.SystemPermissionId into ps
            from p in ps.DefaultIfEmpty()
            select new { r.SystemRoleId, r.SystemRoleCode, r.SystemRoleName, Permission = p == null ? null : p.SystemPermissionCode }).ToListAsync(ct);
        return rows.GroupBy(r => (r.SystemRoleId, r.SystemRoleCode, r.SystemRoleName)).OrderBy(g => g.Key.SystemRoleName)
            .Select(g => new RoleOption(g.Key.SystemRoleId, g.Key.SystemRoleCode, g.Key.SystemRoleName,
                g.Where(x => x.Permission != null).Select(x => x.Permission!).Order().ToArray())).ToList();
    }

    public async Task<TemporaryPasswordResult> Create(CreateEmployeeInput input, InternalUser actor, CancellationToken ct)
    {
        var form = EmployeeAdminRules.Normalize(input);
        await RequireRoles(form.RoleIds!, ct);
        if (await db.Employees.AnyAsync(e => e.EmployeeUsername == form.Username, ct)) throw new InternalAppException(409, "Tên đăng nhập đã được dùng.");
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == form.Code, ct)) throw new InternalAppException(409, "Mã nhân viên đã được dùng.");
        if (form.Email is not null && await db.Employees.AnyAsync(e => e.EmployeeEmail == form.Email, ct)) throw new InternalAppException(409, "Email đã được dùng cho nhân viên khác.");
        if (form.Phone is not null && await db.Employees.AnyAsync(e => e.EmployeePhone == form.Phone, ct)) throw new InternalAppException(409, "Số điện thoại đã được dùng cho nhân viên khác.");

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
            foreach (var roleId in form.RoleIds!)
                db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = employee.EmployeeId, SystemRoleId = roleId, SystemMemberRoleAssignedAt = now, CreatedAt = now, CreatedBy = actor.Id });
            ReviewData.Audit(db, "CREATE", "EMPLOYEE", employee.EmployeeId, "EMPLOYEE", actor.Id, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return new(Row(employee, form.RoleIds!), password);
    }

    public async Task<EmployeeAdminRow> SetRoles(long id, EmployeeRolesInput input, InternalUser actor, CancellationToken ct)
    {
        EmployeeAdminRules.RequireOther(actor, id);
        var roleIds = EmployeeAdminRules.RoleIds(input.RoleIds);
        await RequireRoles(roleIds, ct);
        var employee = await Find(id, ct);
        var now = DateTime.UtcNow;
        var current = await db.EmployeeRoles.Where(r => r.EmployeeId == id).ToListAsync(ct);
        // Roles outside the internal scope (other modules) are left untouched.
        var internalIds = await db.SystemRoles.Where(r => r.SystemRoleScope == InternalScope).Select(r => r.SystemRoleId).ToListAsync(ct);
        db.EmployeeRoles.RemoveRange(current.Where(r => internalIds.Contains(r.SystemRoleId) && !roleIds.Contains(r.SystemRoleId)));
        foreach (var roleId in roleIds.Where(r => current.All(c => c.SystemRoleId != r)))
            db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = id, SystemRoleId = roleId, SystemMemberRoleAssignedAt = now, CreatedAt = now, CreatedBy = actor.Id });
        employee.UpdatedAt = now; employee.UpdatedBy = actor.Id;
        ReviewData.Audit(db, "CHANGE_ROLES", "EMPLOYEE", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return Row(employee, (await RoleIdsByEmployee([id], ct)).GetValueOrDefault(id, []));
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
        return Row(employee, (await RoleIdsByEmployee([id], ct)).GetValueOrDefault(id, []));
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
        return new(Row(employee, (await RoleIdsByEmployee([id], ct)).GetValueOrDefault(id, [])), password);
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

    private async Task RequireRoles(long[] roleIds, CancellationToken ct)
    {
        var found = await db.SystemRoles.CountAsync(r => roleIds.Contains(r.SystemRoleId) && r.SystemRoleScope == InternalScope && r.SystemRoleStatus == EmployeeAdminRules.Active, ct);
        if (found != roleIds.Length) throw new ArgumentException("Vai trò không tồn tại hoặc đã ngừng sử dụng.");
    }

    private async Task<Dictionary<long, long[]>> RoleIdsByEmployee(List<long> ids, CancellationToken ct) =>
        (await (from er in db.EmployeeRoles.AsNoTracking()
                join r in db.SystemRoles on er.SystemRoleId equals r.SystemRoleId
                where ids.Contains(er.EmployeeId) && r.SystemRoleScope == InternalScope
                select new { er.EmployeeId, er.SystemRoleId }).ToListAsync(ct))
            .GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.Select(x => x.SystemRoleId).ToArray());

    private static EmployeeAdminRow Row(Employee e, long[] roleIds) => new(e.EmployeeId, e.EmployeeCode, e.EmployeeUsername, e.EmployeeDisplayName,
        e.EmployeeEmail, e.EmployeePhone, e.EmployeeStatus, e.EmployeeMustChangePassword, e.EmployeeLastLoginAt, roleIds);
}
