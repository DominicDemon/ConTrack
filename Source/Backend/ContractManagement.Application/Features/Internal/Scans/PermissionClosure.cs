using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>
/// Master_Database_HuongDan 2.1: "mở rộng quyền phụ thuộc rồi kiểm tra quyền". A permission that depends
/// on another (SYSTEM_PERMISSION_DEPENDENCY) brings it along, and so on. The table does not stop a cycle,
/// so each permission is visited once.
/// </summary>
public static class PermissionClosure
{
    public static HashSet<long> Expand(IEnumerable<long> granted, IEnumerable<(long Permission, long DependsOn)> dependencies)
    {
        var needs = dependencies.ToLookup(edge => edge.Permission, edge => edge.DependsOn);
        var result = new HashSet<long>();
        var pending = new Stack<long>(granted);
        while (pending.Count > 0)
        {
            var permission = pending.Pop();
            if (!result.Add(permission))
            {
                continue;
            }

            foreach (var required in needs[permission])
            {
                pending.Push(required);
            }
        }

        return result;
    }
}
