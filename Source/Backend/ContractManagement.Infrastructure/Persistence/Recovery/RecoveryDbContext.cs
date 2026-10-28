using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Recovery;

public sealed class RecoveryDbContext(DbContextOptions<RecoveryDbContext> options) : DbContext(options)
{
    public DbSet<AccountRecoveryChallenge> RecoveryChallenges => Set<AccountRecoveryChallenge>();
    public DbSet<AccountRecoveryEmailOutbox> RecoveryEmailOutbox => Set<AccountRecoveryEmailOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        AccountRecoveryMapping.Configure(
            modelBuilder,
            "AccountRecoveryChallenges",
            "AccountRecoveryEmailOutbox",
            "AccountId");
    }
}

