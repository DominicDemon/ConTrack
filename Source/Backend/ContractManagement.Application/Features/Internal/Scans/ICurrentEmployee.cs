using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>The employee the request acts for.</summary>
public interface ICurrentEmployee
{
    long EmployeeId { get; }

    /// <summary>Whether the employee holds the permission, after the permissions it depends on are added.</summary>
    bool HasPermission(string permissionCode);
}
