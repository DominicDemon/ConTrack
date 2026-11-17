using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext
{
    public DbSet<ContractSigningSession> ContractSigningSessions => Set<ContractSigningSession>();
    public DbSet<ContractSigningSigner> ContractSigningSigners => Set<ContractSigningSigner>();
    public DbSet<ContractSigningLink> ContractSigningLinks => Set<ContractSigningLink>();
    public DbSet<ContractSigningOtp> ContractSigningOtps => Set<ContractSigningOtp>();
    public DbSet<ContractSigningAuditLog> ContractSigningAuditLogs => Set<ContractSigningAuditLog>();
    public DbSet<ContractSigningArtifact> ContractSigningArtifacts => Set<ContractSigningArtifact>();

    private static void ConfigureElectronicSigning(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContractSigningSession>(entity =>
        {
            entity.ToTable("tbl_ContractSigningSession", table =>
            {
                table.HasCheckConstraint("CK_ContractSigningSession_Contract", "[ContractId] > 0");
                table.HasCheckConstraint("CK_ContractSigningSession_Version", "[VersionId] > 0");
                table.HasCheckConstraint("CK_ContractSigningSession_Status", "[Status] BETWEEN 0 AND 8");
                table.HasCheckConstraint("CK_ContractSigningSession_LinkLifetime", "[LinkLifetimeHours] BETWEEN 1 AND 168");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ContractId, x.VersionId }).IsUnique();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RejectionReason).HasMaxLength(2000);
            entity.Property(x => x.RejectedAt).HasColumnType("datetime2");
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ContractSigningSigner>(entity =>
        {
            entity.ToTable("tbl_ContractSigningSigner", table =>
            {
                table.HasCheckConstraint("CK_ContractSigningSigner_Role", "[Role] IN (1, 2)");
                table.HasCheckConstraint("CK_ContractSigningSigner_Status", "[Status] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ContractSigningSigner_OtpRequests", "[OtpRequestCount] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SessionId, x.Role }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SignatureStorageKey).HasMaxLength(500);
            entity.Property(x => x.OtpWindowStartedAt).HasColumnType("datetime2");
            entity.Property(x => x.OtpLastRequestedAt).HasColumnType("datetime2");
            entity.Property(x => x.SignatureSha256).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.IpAddress).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.HasOne(x => x.Session).WithMany(x => x.Signers)
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ContractSigningLink>(entity =>
        {
            entity.ToTable("tbl_ContractSigningLink");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.HasOne(x => x.Session).WithMany()
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Signer).WithMany(x => x.Links)
                .HasForeignKey(x => x.SignerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSigningOtp>(entity =>
        {
            entity.ToTable("tbl_ContractSigningOtp");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ChallengeId).IsUnique();
            entity.Property(x => x.ChallengeId).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.OtpHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.OtpCiphertext).HasMaxLength(128);
            entity.Property(x => x.DeliveryStatus).HasMaxLength(16).IsUnicode(false).IsRequired();
            entity.Property(x => x.DeliveryLeaseId).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.DeliveryLastFailure).HasMaxLength(500);
            entity.Property(x => x.DeliveryNextAttemptAt).HasColumnType("datetime2");
            entity.Property(x => x.DeliveryLeaseUntil).HasColumnType("datetime2");
            entity.Property(x => x.ExpiresAt).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ContractSigningOtp_FailedAttempts", "[FailedAttemptCount] BETWEEN 0 AND 5");
                table.HasCheckConstraint("CK_ContractSigningOtp_Expiry", "[ExpiresAt] > [CreatedAt]");
                table.HasCheckConstraint("CK_ContractSigningOtp_DeliveryStatus", "[DeliveryStatus] IN ('Pending', 'Leased', 'Sent', 'Failed', 'Unknown', 'Expired')");
                table.HasCheckConstraint("CK_ContractSigningOtp_DeliveryAttempts", "[DeliveryAttemptCount] >= 0");
            });
            entity.HasIndex(x => new { x.DeliveryStatus, x.DeliveryNextAttemptAt, x.DeliveryLeaseUntil })
                .HasDatabaseName("IX_ContractSigningOtp_DeliveryQueue");
            entity.HasOne(x => x.Signer).WithMany(x => x.Otps)
                .HasForeignKey(x => x.SignerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSigningAuditLog>(entity =>
        {
            entity.ToTable("tbl_ContractSigningAuditLog");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.HasOne(x => x.Session).WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSigningArtifact>(entity =>
        {
            entity.ToTable("tbl_ContractSigningArtifact", table =>
            {
                table.HasCheckConstraint("CK_ContractSigningArtifact_Kind", "[Kind] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_ContractSigningArtifact_Size", "[FileSize] > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SessionId, x.Kind }).IsUnique();
            entity.Property(x => x.TenantCode).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(1000).IsUnicode(false).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.Session).WithMany(x => x.Artifacts)
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
