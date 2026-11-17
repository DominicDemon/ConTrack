using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Central.Configurations;

/// <summary>
/// Cấu hình Fluent API cho các bảng quản trị trung tâm (TENANT, TENANT_DATABASE, EMPLOYEE/SystemAdmin, AUDIT_LOG) trong MasterDB.
/// </summary>
internal static class CentralCoreModelConfiguration
{
    public static void ConfigureAll(ModelBuilder modelBuilder)
    {
        ConfigureTenant(modelBuilder);
        ConfigureTenantDatabase(modelBuilder);
        ConfigureSystemAdmin(modelBuilder);
        ConfigureSecurityAudit(modelBuilder);
    }

    private static void ConfigureSecurityAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CentralSecurityAudit>(entity =>
        {
            entity.ToTable("AUDIT_LOG");
            entity.HasKey(x => x.CentralSecurityAuditId);
            entity.Property(x => x.CentralSecurityAuditId).HasColumnName("audit_log_id");
            entity.Property(x => x.Action).HasColumnName("action_type").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.TargetType).HasColumnName("entity_name").HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.TargetId)
                .HasColumnName("entity_id")
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasConversion(
                    v => string.IsNullOrEmpty(v) ? "N/A" : v,
                    v => v);
            entity.Property(x => x.Result).HasColumnName("actor_type").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ActorSystemAdminId).HasColumnName("actor_id").HasConversion<long?>();
            entity.Property(x => x.IpAddress).HasColumnName("actor_ip_address").HasMaxLength(45).IsUnicode(false);
            entity.Property(x => x.ChangedFields).HasColumnName("new_data");
            entity.Property(x => x.OccurredAt).HasColumnName("created_at").HasColumnType("datetime2");

            entity.Ignore(x => x.TenantId);
            entity.Ignore(x => x.TenantCode);
            entity.Ignore(x => x.FailureCode);
            entity.Ignore(x => x.UserAgent);
            entity.Ignore(x => x.CorrelationId);
        });
    }

    private static void ConfigureSystemAdmin(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemAdmin>(entity =>
        {
            entity.ToTable("EMPLOYEE");

            entity.HasKey(x => x.SystemAdminId);

            entity.Property(x => x.SystemAdminId)
                .HasColumnName("employee_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();

            entity.Property<string>("EmployeeCode")
                .HasColumnName("employee_code")
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("SYSADMIN");

            entity.Property(x => x.Username)
                .HasColumnName("employee_username")
                .HasMaxLength(100)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasColumnName("employee_password_hash")
                .HasMaxLength(255)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(x => x.FullName)
                .HasColumnName("employee_display_name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasColumnName("employee_email")
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.Property(x => x.RecoveryEmailVerifiedAt)
                .HasColumnName("employee_email_verified_at")
                .HasColumnType("datetime2");

            entity.Property(x => x.IsActive)
                .HasColumnName("employee_status")
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasConversion(
                    v => v ? "ACTIVE" : "INACTIVE",
                    v => string.IsNullOrWhiteSpace(v)
                        || string.Equals(v, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                        || v == "1")
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime2");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime2");

            entity.Property(x => x.PasswordChangedAt)
                .HasColumnName("employee_last_login_at")
                .HasColumnType("datetime2");

            entity.Ignore(x => x.MustChangePassword);
            entity.Ignore(x => x.SessionVersion);
            entity.Ignore(x => x.AvatarStorageKey);
            entity.Ignore(x => x.AvatarContentType);
            entity.Ignore(x => x.AvatarFileSize);
            entity.Ignore(x => x.AvatarSha256);
            entity.Ignore(x => x.AvatarUpdatedAt);
            entity.Ignore(x => x.CoverStorageKey);
            entity.Ignore(x => x.CoverContentType);
            entity.Ignore(x => x.CoverFileSize);
            entity.Ignore(x => x.CoverSha256);
            entity.Ignore(x => x.CoverUpdatedAt);
            entity.Ignore(x => x.RowVersion);
        });
    }

    private static void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("TENANT");

            entity.HasKey(x => x.TenantId);

            entity.Property(x => x.TenantId)
                .HasColumnName("tenant_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();

            entity.Property(x => x.OwnerCustomerId)
                .HasColumnName("customer_id");

            entity.Property<long>("SaasTypeId")
                .HasColumnName("saas_type_id")
                .HasDefaultValue(1L);

            entity.Property<long>("PackageLevelId")
                .HasColumnName("package_level_id")
                .HasDefaultValue(1L);

            entity.Property(x => x.TenantCode)
                .HasColumnName("tenant_code")
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(x => x.TenantName)
                .HasColumnName("tenant_name")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property<string>("TenantSubdomain")
                .HasColumnName("tenant_subdomain")
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValue("default");

            entity.Property(x => x.Status)
                .HasColumnName("tenant_status")
                .HasConversion(
                    v => v == TenantStatus.Active
                        ? "ACTIVE"
                        : v == TenantStatus.Provisioning
                            ? "PENDING"
                            : v == TenantStatus.Suspended
                                ? "SUSPENDED"
                                : "FAILED",
                    v => string.Equals(v, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                        ? TenantStatus.Active
                        : string.Equals(v, "PENDING", StringComparison.OrdinalIgnoreCase)
                            ? TenantStatus.Provisioning
                            : string.Equals(v, "SUSPENDED", StringComparison.OrdinalIgnoreCase)
                                ? TenantStatus.Suspended
                                : TenantStatus.Failed)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime2");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime2");

            entity.Ignore(x => x.ProvisioningError);
            entity.Ignore(x => x.TenantDatabaseId);
            entity.Ignore(x => x.TenantDatabase);

            entity.HasIndex(x => x.TenantCode)
                .IsUnique();
        });
    }

    private static void ConfigureTenantDatabase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantDatabase>(entity =>
        {
            entity.ToTable("TENANT_DATABASE");

            entity.HasKey(x => x.TenantDatabaseId);

            entity.Property(x => x.TenantDatabaseId)
                .HasColumnName("tenant_database_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();

            entity.Property(x => x.DatabaseName)
                .HasColumnName("tenant_database_db_name")
                .HasMaxLength(255)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime2");

            entity.Ignore(x => x.DatabaseKey);
            entity.Ignore(x => x.ConnectionString);
            entity.Ignore(x => x.Mode);
            entity.Ignore(x => x.Tenants);
        });
    }
}

