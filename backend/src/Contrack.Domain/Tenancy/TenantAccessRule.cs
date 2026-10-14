namespace Contrack.Domain.Tenancy;

/// <summary>"Only Active tenants can be accessed by users" (SRS I.3.3).</summary>
public static class TenantAccessRule
{
    public static bool CanAccess(TenantStatus status) => status == TenantStatus.Active;
}
