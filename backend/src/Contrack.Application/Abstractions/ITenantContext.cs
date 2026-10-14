namespace Contrack.Application.Abstractions;

/// <summary>
/// Tenant của request hiện tại. TenantResolutionMiddleware (Sprint 0, task "Tenant resolver")
/// sẽ điền giá trị từ subdomain hoặc header X-Tenant khi chạy local.
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }
    string? TenantCode { get; }
    string? ConnectionString { get; }
}
