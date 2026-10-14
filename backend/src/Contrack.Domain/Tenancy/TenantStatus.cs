namespace Contrack.Domain.Tenancy;

/// <summary>Vòng đời tenant theo Data Business Rule "Tenant Lifecycle Status".</summary>
public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Terminated = 3
}
