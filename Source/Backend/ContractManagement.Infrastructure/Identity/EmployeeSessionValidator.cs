using ContractManagement.API.Common.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Common.Security;

/// <summary>One employee-session check for MVC requests and SignalR handshakes.</summary>
public static class EmployeeSessionValidator
{
    public static async Task<AuthenticatedEmployee> ValidateAsync(
        HttpContext httpContext, ICurrentTenant currentTenant, DbDtctechContext dbContext,
        IReadOnlyCollection<string> requiredPermissions, bool allowWhenPasswordChangeRequired = false)
    {
        var session = httpContext.Session;
        var employeeId = session.GetInt32(AccountSessionKeys.EmployeeId);
        var sessionVersion = session.GetInt32(AccountSessionKeys.EmployeeSessionVersion);
        var tenantId = session.GetInt32("TenantId");
        if (employeeId is null || sessionVersion is null || tenantId is null)
            throw Deny(401, AuthorizationErrorCodes.AuthenticationRequired, "Employee login is required.");
        if (!currentTenant.IsResolved || currentTenant.Value!.TenantId != tenantId)
        {
            ClearSession(httpContext, employeeId.Value);
            throw Deny(401, AuthorizationErrorCodes.AuthenticationRequired, "Employee session is not valid for the current tenant.");
        }
        var actorKind = session.GetString("ActorKind");
        TblEmployee? employeeEntity = null;
        if (string.Equals(actorKind, "Customer", StringComparison.OrdinalIgnoreCase) || employeeId.Value < 0)
        {
            var custId = Math.Abs(employeeId.Value);
            var customerEntity = await dbContext.TblCustomers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == custId, httpContext.RequestAborted);
            if (customerEntity is not null)
            {
                var mappedAcct = customerEntity.CustomerEmail ?? customerEntity.CustomerPhone ?? $"cust.{customerEntity.CustomerId}";
                employeeEntity = new TblEmployee
                {
                    EmployeeId = customerEntity.CustomerId,
                    EmployeeAccount = mappedAcct,
                    EmployeeFullName = customerEntity.CustomerFullName ?? mappedAcct,
                    EmployeeType = 6,
                    Status = customerEntity.Status == 0 ? (byte)0 : (byte)1,
                    MustChangePassword = false,
                    SessionVersion = sessionVersion.Value
                };
            }
        }
        else
        {
            employeeEntity = await dbContext.TblEmployees.AsNoTracking()
                .FirstOrDefaultAsync(x => x.EmployeeId == employeeId.Value, httpContext.RequestAborted);
        }
        var employee = employeeEntity is null
            ? null
            : new
            {
                employeeEntity.EmployeeId,
                Account = employeeEntity.EmployeeAccount,
                FullName = employeeEntity.EmployeeFullName,
                employeeEntity.EmployeeType,
                employeeEntity.Status,
                employeeEntity.MustChangePassword,
                employeeEntity.SessionVersion,
                employeeEntity.PasswordChangedAt,
                employeeEntity.DefaultPage,
                employeeEntity.AvatarStorageKey,
                employeeEntity.AvatarUpdatedAt
            };
        if (employee is null)
        {
            ClearSession(httpContext, employeeId.Value);
            throw Deny(401, AuthorizationErrorCodes.AuthenticationRequired, "Employee session is no longer valid.");
        }
        if (employee.Status != 1)
        {
            ClearSession(httpContext, employee.EmployeeId);
            throw Deny(401, AuthorizationErrorCodes.EmployeeInactive, "Employee account is inactive.");
        }
        if (employee.SessionVersion != sessionVersion)
        {
            ClearSession(httpContext, employee.EmployeeId);
            throw Deny(401, AuthorizationErrorCodes.AuthenticationRequired, "Employee session has expired.");
        }
        if (!EmployeePermissionCatalog.TryGetPermissions(employee.EmployeeType, out var permissions))
            throw Deny(403, AuthorizationErrorCodes.PermissionDenied, $"Employee role is not valid for {RbacPermissions.Version}.");
        if (employee.MustChangePassword && !allowWhenPasswordChangeRequired)
            throw Deny(403, AuthorizationErrorCodes.MustChangePassword, "Bạn phải đổi mật khẩu trước khi tiếp tục.");
        if (requiredPermissions.Any(required => !permissions.Contains(required, StringComparer.Ordinal)))
            throw Deny(403, AuthorizationErrorCodes.PermissionDenied, "Employee does not have the required permission.");
        var authenticated = new AuthenticatedEmployee(employee.EmployeeId, employee.Account, employee.FullName,
            (EmployeeType)employee.EmployeeType!.Value, permissions, employee.MustChangePassword,
            employee.PasswordChangedAt,
            employee.AvatarStorageKey is null ? null : $"/api/auth/profile/avatar?v={employee.AvatarUpdatedAt?.Ticks ?? 0}",
            employee.DefaultPage);
        httpContext.Items[EmployeeAuthorizationContext.HttpContextItemKey] = authenticated;
        return authenticated;
    }

    private static void ClearSession(HttpContext context, int employeeId)
    {
        context.Items[SecurityAuditHttpContextItems.DeniedActorEmployeeIdKey] = employeeId;
        context.Session.Clear();
    }

    private static RbacOperationException Deny(int status, string code, string message) => new(status, code, message);
}
