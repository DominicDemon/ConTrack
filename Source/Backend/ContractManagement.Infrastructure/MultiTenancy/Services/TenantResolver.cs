using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContractManagement.Infrastructure.MultiTenancy.Services;

/// <summary>
/// Tìm tenant và connection string trong Central Database.
/// </summary>
public sealed class TenantResolver : ITenantResolver
{
    private readonly CentralDbContext _centralDbContext;
    private readonly IConfiguration? _configuration;

    public TenantResolver(
        CentralDbContext centralDbContext,
        IConfiguration? configuration = null)
    {
        _centralDbContext = centralDbContext;
        _configuration = configuration;
    }

    public async Task<ResolvedTenant?> ResolveAsync(
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            return null;
        }

        string normalizedTenantCode =
            tenantCode.Trim().ToLowerInvariant();

        var defaultConnectionString =
            _configuration?.GetConnectionString("DefaultConnection")
            ?? _configuration?.GetConnectionString("CentralDatabase")
            ?? "server=localhost;database=MasterDB;uid=sa;pwd=123;TrustServerCertificate=True;Encrypt=False;";

        /*
         * AsNoTracking:
         * Chỉ đọc dữ liệu, không cần EF theo dõi thay đổi.
         */
        var tenant = await _centralDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantCode == normalizedTenantCode
                    && x.Status == TenantStatus.Active,
                cancellationToken);

        if (tenant is not null)
        {
            var connStr = !string.IsNullOrWhiteSpace(tenant.TenantDatabase?.ConnectionString)
                ? tenant.TenantDatabase.ConnectionString
                : defaultConnectionString;
            return new ResolvedTenant(
                tenant.TenantId,
                tenant.TenantCode,
                tenant.TenantName,
                tenant.TenantDatabase?.Mode ?? TenantDatabaseMode.Shared,
                connStr);
        }

        var firstActive = await _centralDbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.TenantId)
            .FirstOrDefaultAsync(x => x.Status == TenantStatus.Active, cancellationToken);

        if (firstActive is not null)
        {
            return new ResolvedTenant(
                firstActive.TenantId,
                normalizedTenantCode,
                firstActive.TenantName,
                TenantDatabaseMode.Shared,
                defaultConnectionString);
        }

        return new ResolvedTenant(
            1,
            normalizedTenantCode,
            normalizedTenantCode.ToUpperInvariant(),
            TenantDatabaseMode.Shared,
            defaultConnectionString);
    }
}