using Microsoft.EntityFrameworkCore;

namespace Contrack.Infrastructure.Persistence;

/// <summary>
/// Master DB (SRS I.3.1.1): khách hàng SaaS, gói dịch vụ, đơn hàng, tenant, tenant_databases...
/// Cấu hình entity đặt trong namespace Contrack.Infrastructure.Persistence.Master.
/// </summary>
public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MasterDbContext).Assembly,
            type => type.Namespace?.EndsWith(".Persistence.Master", StringComparison.Ordinal) == true);
        base.OnModelCreating(modelBuilder);
    }
}
