using Microsoft.EntityFrameworkCore;

namespace Contrack.Infrastructure.Persistence;

/// <summary>
/// Tenant DB (SRS I.3.1.2): mỗi doanh nghiệp một database riêng (BR-21).
/// Connection string lấy theo tenant của request; sẽ đăng ký ở task "Tenant resolver".
/// Cấu hình entity đặt trong namespace Contrack.Infrastructure.Persistence.Tenant.
/// </summary>
public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TenantDbContext).Assembly,
            type => type.Namespace?.EndsWith(".Persistence.Tenant", StringComparison.Ordinal) == true);
        base.OnModelCreating(modelBuilder);
    }
}
