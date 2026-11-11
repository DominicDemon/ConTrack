using ContractManagement.Infrastructure.Persistence.Central.Configurations;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Central;

/// <summary>
/// DbContext quản lý dữ liệu SaaS & Tenant Registry trên MasterDB (Clean Code).
/// Toàn bộ cấu hình Fluent API được tách ra thư mục Configurations/ và Saas/.
/// </summary>
public sealed class CentralDbContext : DbContext
{
    public CentralDbContext(DbContextOptions<CentralDbContext> options)
        : base(options)
    {
    }

    public DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();
    public DbSet<AccountRecoveryChallenge> RecoveryChallenges => Set<AccountRecoveryChallenge>();
    public DbSet<AccountRecoveryEmailOutbox> RecoveryEmailOutbox => Set<AccountRecoveryEmailOutbox>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();
    public DbSet<CentralSecurityAudit> SecurityAudits => Set<CentralSecurityAudit>();

    public DbSet<SaasCustomer> Customers => Set<SaasCustomer>();
    public DbSet<SaasCustomerBusiness> CustomerBusinesses => Set<SaasCustomerBusiness>();
    public DbSet<SaasType> SaasTypes => Set<SaasType>();
    public DbSet<SaasPlan> Plans => Set<SaasPlan>();
    public DbSet<SaasPlanVersion> PlanVersions => Set<SaasPlanVersion>();
    public DbSet<SaasSubscription> Subscriptions => Set<SaasSubscription>();
    public DbSet<SaasPromotion> Promotions => Set<SaasPromotion>();
    public DbSet<SaasInvoice> SaasInvoices => Set<SaasInvoice>();
    public DbSet<SaasPayment> SaasPayments => Set<SaasPayment>();
    public DbSet<SaasInvoicePromotionDetail> InvoicePromotionDetails => Set<SaasInvoicePromotionDetail>();
    public DbSet<SaasSubscriptionPromotionDetail> SubscriptionPromotionDetails => Set<SaasSubscriptionPromotionDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        CentralCoreModelConfiguration.ConfigureAll(modelBuilder);
        AccountRecoveryMapping.Configure(
            modelBuilder,
            "AccountRecoveryChallenges",
            "AccountRecoveryEmailOutbox",
            "SystemAdminId");

        modelBuilder.ApplySaasModel();
        modelBuilder.Entity<Tenant>()
            .HasOne<SaasCustomer>()
            .WithMany()
            .HasForeignKey(x => x.OwnerCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        SaasSeedData.Apply(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateSecurityAudits();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateSecurityAudits();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateSecurityAudits()
    {
        if (ChangeTracker.Entries<CentralSecurityAudit>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Central security audit is append-only and cannot be updated or deleted.");
        }
    }
}
