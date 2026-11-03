using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContractManagement.Infrastructure.Persistence.Master;

public static class MasterDatabaseRegistration
{
    public static IServiceCollection AddMasterDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();
        var connectionString = configuration.GetConnectionString("MasterDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddDbContext<MasterDbContext>(options => options.UseSqlServer(
            connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddHealthChecks().AddCheck<MasterDatabaseHealthCheck>(
            "master-database", tags: ["master"]);
        return services;
    }
}

internal sealed class MasterDatabaseHealthCheck(MasterDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check the new schema as well as connectivity. Never initialize or migrate here.
            await db.ContractDocuments.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.Employees.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.Tenants.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("MASTER database is unavailable.", exception);
        }
    }
}
