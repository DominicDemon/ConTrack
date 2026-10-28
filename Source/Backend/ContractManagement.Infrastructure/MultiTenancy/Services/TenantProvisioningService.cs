using ContractManagement.Infrastructure.DatabaseScripts.SeedData;
using ContractManagement.Infrastructure.MultiTenancy.Contracts;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Options;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.MultiTenancy.Services;

/// <summary>
/// Tạo tenant và database riêng cho tenant.
/// </summary>
public sealed class TenantProvisioningService
    : ITenantProvisioningService
{
    private readonly CentralDbContext _centralDbContext;
    private readonly ITenantDatabaseInitializer _databaseInitializer;
    private readonly MultiTenancyOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantProvisioningService> _logger;
    private readonly ITenantSeedData _tenantSeedData;

    public TenantProvisioningService(
        CentralDbContext centralDbContext,
        ITenantDatabaseInitializer databaseInitializer,
        IOptions<MultiTenancyOptions> options,
        IConfiguration configuration,
        ILogger<TenantProvisioningService> logger,
        ITenantSeedData tenantSeedData)
    {
        _centralDbContext = centralDbContext;
        _databaseInitializer = databaseInitializer;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
        _tenantSeedData = tenantSeedData;
    }

    public async Task<IReadOnlyList<TenantProvisioningResult>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _centralDbContext.Tenants
            .AsNoTracking()
            .Include(tenant => tenant.TenantDatabase)
            .OrderBy(tenant => tenant.TenantCode)
            .Select(tenant => new TenantProvisioningResult(
                tenant.TenantId,
                tenant.TenantCode,
                tenant.TenantName,
                tenant.TenantDatabase.DatabaseName,
                tenant.TenantDatabase.Mode,
                tenant.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantProvisioningResult>
        CreateDedicatedAsync(
            TenantProvisioningCommand command,
            CancellationToken cancellationToken = default)
    {
        string tenantCode = command.TenantCode.Trim();
        if (!PasswordRules.IsValid(command.InitialManager.EmployeePassword))
            throw new ArgumentException(PasswordRules.ValidationMessage, nameof(command));
        if (tenantCode.Length is < 3 or > 50
            || tenantCode.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character != '-'))
        {
            throw new ArgumentException(
                "Mã tenant phải dài 3–50 ký tự và chỉ chứa chữ cái, số hoặc dấu gạch ngang.",
                nameof(command));
        }
        string normalizedTenantCode = tenantCode.ToLowerInvariant();
        string tenantName = command.TenantName.Trim();
        if (tenantName.Length is < 1 or > 500)
        {
            throw new ArgumentException(
                "Tên tổ chức phải có từ 1 đến 500 ký tự.", nameof(command));
        }

        /*
         * Kiểm tra tenant đã tồn tại chưa.
         */
        var existingTenant =
            await _centralDbContext.Tenants
                .Include(x => x.TenantDatabase)
                .SingleOrDefaultAsync(
                    x => x.TenantCode.ToLower() == normalizedTenantCode,
                    cancellationToken);

        if (existingTenant is not null
            && existingTenant.Status != TenantStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantCode}' đã tồn tại.");
        }

        Tenant tenant;
        TenantDatabase tenantDatabase;
        string connectionString;

        if (existingTenant is not null)
        {
            /*
             * Retry tenant Failed: dùng lại bản ghi tenant và database cũ
             * (initializer chạy migrate nên an toàn khi database đã tạo dở).
             */
            tenant = existingTenant;
            tenantDatabase = existingTenant.TenantDatabase;
            connectionString = tenantDatabase.ConnectionString;
            tenant.TenantName = tenantName;
            tenant.Status = TenantStatus.Provisioning;
            tenant.ProvisioningError = null;
            tenant.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            /*
             * DatabaseKey và tên database dùng mã chuẩn hóa chữ thường,
             * TenantCode hiển thị vẫn giữ đúng chữ hoa/thường được nhập.
             *
             * Khi đặt tên database, đổi dấu gạch ngang thành gạch dưới.
             */
            string safeSuffix =
                normalizedTenantCode.Replace("-", "_");

            string databaseName =
                $"{_options.DatabasePrefix}{safeSuffix}";

            connectionString =
                BuildConnectionString(databaseName);

            tenantDatabase = new TenantDatabase
            {
                DatabaseKey = $"dedicated-{normalizedTenantCode}",
                DatabaseName = databaseName,
                ConnectionString = connectionString,
                Mode = TenantDatabaseMode.Dedicated,
                CreatedAt = DateTime.UtcNow
            };

            tenant = new Tenant
            {
                TenantCode = tenantCode,
                TenantName = tenantName,
                Status = TenantStatus.Provisioning,
                TenantDatabase = tenantDatabase,
                CreatedAt = DateTime.UtcNow
            };

            /*
             * Lưu trạng thái Provisioning trước khi tạo database.
             *
             * Nếu quá trình tạo database lỗi,
             * hệ thống vẫn có bản ghi để kiểm tra và retry.
             */
            _centralDbContext.Tenants.Add(tenant);
        }

        await _centralDbContext.SaveChangesAsync(
            cancellationToken);

        try
        {
            await _databaseInitializer.InitializeAsync(
                connectionString,
                cancellationToken);
            
            var initialManagerId = await _tenantSeedData.InitializeAsync(
                connectionString,
                tenant.TenantId,
                command.InitialManager,
                command.SecurityContext,
                cancellationToken);

            tenant.Status = TenantStatus.Active;
            // Persist the exact purchaser so fulfillment can recover after a process restart.
            var correlation = command.SecurityContext.CorrelationId;
            if (correlation?.StartsWith("subscription-", StringComparison.Ordinal) == true
                && Guid.TryParse(correlation[13..], out var subscriptionPublicId))
            {
                var subscription = await _centralDbContext.Subscriptions.SingleAsync(
                    s => s.PublicId == subscriptionPublicId && s.RequestedTenantCode == tenant.TenantCode,
                    cancellationToken);
                subscription.TenantId = tenant.TenantId;
                subscription.PurchaserEmployeeId = initialManagerId;
                tenant.OwnerCustomerId = subscription.CustomerId;
            }
            tenant.ProvisioningError = null;
            tenant.UpdatedAt = DateTime.UtcNow;

            _centralDbContext.SecurityAudits.Add(
                AuthorizationAuditRecordFactory.CreateCentral(
                    command.SecurityContext.SystemAdminId,
                    tenant.TenantId,
                    tenant.TenantCode,
                    AuthorizationAuditActionTypes.TenantProvisioned,
                    AuthorizationAuditResultTypes.Success,
                    "Tenant",
                    tenant.TenantId.ToString(),
                    null,
                    6,
                    null,
                    1,
                    null,
                    DateTime.UtcNow,
                    command.SecurityContext.IpAddress,
                    command.SecurityContext.UserAgent,
                    command.SecurityContext.CorrelationId));

            await _centralDbContext.SaveChangesAsync(
                cancellationToken);

            return new TenantProvisioningResult(
                tenant.TenantId,
                tenant.TenantCode,
                tenant.TenantName,
                tenantDatabase.DatabaseName,
                tenantDatabase.Mode,
                tenant.Status, initialManagerId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Tạo database cho tenant {TenantCode} thất bại.",
                tenantCode);

            tenant.Status = TenantStatus.Failed;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.ProvisioningError =
                Truncate(exception.Message, 2000);

            _centralDbContext.SecurityAudits.Add(
                AuthorizationAuditRecordFactory.CreateCentral(
                    command.SecurityContext.SystemAdminId,
                    tenant.TenantId,
                    tenant.TenantCode,
                    AuthorizationAuditActionTypes.TenantProvisioned,
                    AuthorizationAuditResultTypes.Failed,
                    "Tenant",
                    tenant.TenantId.ToString(),
                    null,
                    null,
                    null,
                    null,
                    "ProvisioningFailed",
                    DateTime.UtcNow,
                    command.SecurityContext.IpAddress,
                    command.SecurityContext.UserAgent,
                    command.SecurityContext.CorrelationId));

            await _centralDbContext.SaveChangesAsync(
                cancellationToken);

            throw;
        }
    }

    private string BuildConnectionString(
        string databaseName)
    {
        string templateConnectionString =
            _configuration.GetConnectionString(
                _options.TemplateConnectionName)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy connection string "
                + $"'{_options.TemplateConnectionName}'.");

        /*
         * Không sử dụng string.Replace để thay tên database.
         *
         * SqlConnectionStringBuilder hiểu đúng cấu trúc
         * connection string của SQL Server.
         */
        var connectionStringBuilder =
            new SqlConnectionStringBuilder(
                templateConnectionString)
            {
                InitialCatalog = databaseName
            };

        return connectionStringBuilder.ConnectionString;
    }

    private static string Truncate(
        string value,
        int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
