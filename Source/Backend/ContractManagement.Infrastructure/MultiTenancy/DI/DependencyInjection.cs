using ContractManagement.Application.Common.Interfaces;
using ContractManagement.Infrastructure.DatabaseScripts.SeedData;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Options;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractManagement.Infrastructure.MultiTenancy.DI;

/// <summary>
/// Đăng ký tập trung toàn bộ DbContext và Multi-Tenancy Services của Infrastructure.
/// - Mặc định sử dụng MasterDB (CentralDatabase / DefaultConnection / MasterDatabase).
/// - Khi phát triển thêm Tenant Database riêng, DbDtctechContext tự động chuyển đổi connection string theo CurrentTenant.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddContractManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MultiTenancyOptions>(
            configuration.GetSection(MultiTenancyOptions.SectionName));

        services.AddOptions<SystemAdminBootstrapOptions>()
            .Bind(configuration.GetSection(SystemAdminBootstrapOptions.SectionName))
            .Validate(
                options => !options.Enabled
                    || (!string.IsNullOrWhiteSpace(options.Username)
                        && ContractManagement.Infrastructure.Security.PasswordRules.IsValid(options.Password)
                        && !string.IsNullOrWhiteSpace(options.FullName)
                        && !string.IsNullOrWhiteSpace(options.Email)),
                "SystemAdminBootstrap requires username, a 12–100 character password containing a digit, uppercase and lowercase letters, full name and email.")
            .ValidateOnStart();

        var masterConnectionString = ResolveMasterConnectionString(configuration);
        var migrationsAssembly = typeof(DbDtctechContext).Assembly.GetName().Name;

        // 1. CentralDbContext: Luôn kết nối vào MasterDB để tra cứu Tenant, gói dịch vụ SaaS & SystemAdmin
        services.AddDbContext<CentralDbContext>(options =>
        {
            options.UseSqlServer(
                masterConnectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                    sqlOptions.MigrationsAssembly(migrationsAssembly);
                });
        });

        // 2. Scoped Tenant Resolution cho mỗi HTTP Request
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<ITenantResolver, TenantResolver>();

        // 3. Unified Application & Tenant DbContext (DbDtctechContext / ApplicationDbContext / IApplicationDbContext):
        //    - Hiện tại: kết nối trực tiếp vào MasterDB.
        //    - Tương lai: nếu Tenant được gán TenantDatabase riêng, tự động chuyển sang ConnectionString của Tenant đó.
        services.AddDbContext<DbDtctechContext>((serviceProvider, options) =>
        {
            var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
            var targetConnectionString = ResolveTenantOrMasterConnectionString(
                currentTenant,
                configuration,
                masterConnectionString);

            options.UseSqlServer(
                targetConnectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                    sqlOptions.MigrationsAssembly(migrationsAssembly);
                });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DbDtctechContext>());
        services.AddScoped<ApplicationDbContext>(sp => new ApplicationDbContext(
            sp.GetRequiredService<DbContextOptions<DbDtctechContext>>()));

        // 4. Tenant Provisioning & Seed Data Services
        services.AddScoped<ITenantDatabaseInitializer, EfCoreTenantDatabaseInitializer>();
        services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        services.AddScoped<IPasswordHasher<TblEmployee>, PasswordHasher<TblEmployee>>();
        services.AddScoped<ITenantSeedData, TenantSeedData>();
        services.AddScoped<IPasswordHasher<SystemAdmin>, PasswordHasher<SystemAdmin>>();
        services.AddScoped<ICentralSeedData, CentralSeedData>();

        return services;
    }

    private static string ResolveMasterConnectionString(IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("CentralDatabase")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("MasterDatabase")
            ?? throw new InvalidOperationException(
                "Không tìm thấy connection string 'CentralDatabase', 'DefaultConnection' hoặc 'MasterDatabase'.");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:CentralDatabase chưa được cấu hình.");
        }

        return connectionString;
    }

    private static string ResolveTenantOrMasterConnectionString(
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        string masterConnectionString)
    {
        if (currentTenant.IsResolved
            && !string.IsNullOrWhiteSpace(currentTenant.Value?.ConnectionString))
        {
            return currentTenant.Value!.ConnectionString;
        }

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        return !string.IsNullOrWhiteSpace(defaultConnection)
            ? defaultConnection
            : masterConnectionString;
    }
}
