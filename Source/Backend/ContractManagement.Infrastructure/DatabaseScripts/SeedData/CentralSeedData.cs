using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.DatabaseScripts.SeedData;

public sealed class CentralSeedData : ICentralSeedData
{
    private readonly CentralDbContext _centralDbContext;
    private readonly IPasswordHasher<SystemAdmin> _passwordHasher;
    private readonly SystemAdminBootstrapOptions _options;
    private readonly ILogger<CentralSeedData> _logger;

    public CentralSeedData(
        CentralDbContext centralDbContext,
        IPasswordHasher<SystemAdmin> passwordHasher,
        IOptions<SystemAdminBootstrapOptions> options,
        ILogger<CentralSeedData> logger)
    {
        _centralDbContext = centralDbContext;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var normalizedUsername = _options.Username.Trim();
        if (!ContractManagement.Infrastructure.Security.PasswordRules.IsValid(_options.Password))
            throw new InvalidOperationException(ContractManagement.Infrastructure.Security.PasswordRules.ValidationMessage);

        try
        {
            try
            {
                await _centralDbContext.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE [EMPLOYEE]
                    SET [employee_personal_identifier] = 'ID-' + CAST([employee_id] AS VARCHAR(15))
                    WHERE [employee_personal_identifier] IS NULL;

                    UPDATE [EMPLOYEE]
                    SET [employee_phone] = '0900' + RIGHT('000000' + CAST([employee_id] AS VARCHAR(10)), 6)
                    WHERE [employee_phone] IS NULL;

                    UPDATE [CUSTOMER]
                    SET [customer_password_hash] = 'AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxAzPX7zO3S/k6l2cP8qipbqXU9ql4+4uka6Dxa0p+t20w=='
                    WHERE [customer_password_hash] IS NULL;

                    DECLARE @SeedHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxAzPX7zO3S/k6l2cP8qipbqXU9ql4+4uka6Dxa0p+t20w==';
                    DECLARE @SeedNow DATETIME2 = SYSUTCDATETIME();

                    IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.sale' OR [employee_code] = 'HEAD-SALE01')
                        INSERT INTO [EMPLOYEE] ([employee_code], [employee_email], [employee_phone], [employee_password_hash], [employee_display_name], [employee_username], [employee_personal_identifier], [employee_status], [employee_dob], [employee_gender], [employee_address], [employee_email_verified_at], [employee_last_login_at], [created_at])
                        VALUES ('HEAD-SALE01', 'head.sale@contrack.vn', '0901000013', @SeedHash, N'Hoàng Trưởng Phòng Kinh Doanh', 'head.sale', '001099000013', 'ACTIVE', '1989-06-15', 'MALE', N'TP.HCM', @SeedNow, @SeedNow, @SeedNow);

                    IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.accountant' OR [employee_code] = 'HEAD-ACC01')
                        INSERT INTO [EMPLOYEE] ([employee_code], [employee_email], [employee_phone], [employee_password_hash], [employee_display_name], [employee_username], [employee_personal_identifier], [employee_status], [employee_dob], [employee_gender], [employee_address], [employee_email_verified_at], [employee_last_login_at], [created_at])
                        VALUES ('HEAD-ACC01', 'head.accountant@contrack.vn', '0901000014', @SeedHash, N'Đặng Trưởng Phòng Kế Toán', 'head.accountant', '001099000014', 'ACTIVE', '1988-09-20', 'FEMALE', N'Hà Nội', @SeedNow, @SeedNow, @SeedNow);

                    IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.legal' OR [employee_code] = 'HEAD-LEGAL01')
                        INSERT INTO [EMPLOYEE] ([employee_code], [employee_email], [employee_phone], [employee_password_hash], [employee_display_name], [employee_username], [employee_personal_identifier], [employee_status], [employee_dob], [employee_gender], [employee_address], [employee_email_verified_at], [employee_last_login_at], [created_at])
                        VALUES ('HEAD-LEGAL01', 'head.legal@contrack.vn', '0901000015', @SeedHash, N'Vũ Trưởng Phòng Hành Chính - Pháp Chế', 'head.legal', '001099000015', 'ACTIVE', '1987-12-10', 'MALE', N'Hà Nội', @SeedNow, @SeedNow, @SeedNow);

                    IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.legal' OR [employee_code] = 'EMP-LEGAL01')
                        INSERT INTO [EMPLOYEE] ([employee_code], [employee_email], [employee_phone], [employee_password_hash], [employee_display_name], [employee_username], [employee_personal_identifier], [employee_status], [employee_dob], [employee_gender], [employee_address], [employee_email_verified_at], [employee_last_login_at], [created_at])
                        VALUES ('EMP-LEGAL01', 'legal@contrack.vn', '0901000016', @SeedHash, N'Bùi Chuyên Viên Pháp Chế', 'test.legal', '001099000016', 'ACTIVE', '1996-04-18', 'FEMALE', N'Hà Nội', @SeedNow, @SeedNow, @SeedNow);

                    DECLARE @MgrId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.manager');
                    DECLARE @TechId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.technical');
                    DECLARE @HeadSaleId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.sale');
                    DECLARE @SaleId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.sale');
                    DECLARE @HeadAccId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.accountant');
                    DECLARE @AccId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.accountant');
                    DECLARE @HeadLegalId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.legal');
                    DECLARE @LegalId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.legal');

                    -- Trưởng phòng Kỹ Thuật & Hỗ Trợ (Department 2): test.manager
                    IF @MgrId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @MgrId AND [department_id] = 2)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@MgrId, 2, @SeedNow);
                    IF @TechId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @TechId AND [department_id] = 2)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@TechId, 2, @SeedNow);

                    -- Phòng Kinh Doanh (Department 3): head.sale (Trưởng phòng), test.sale (Nhân viên)
                    IF @HeadSaleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @HeadSaleId AND [department_id] = 3)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@HeadSaleId, 3, @SeedNow);
                    IF @SaleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @SaleId AND [department_id] = 3)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@SaleId, 3, @SeedNow);

                    -- Phòng Kế Toán - Tài Chính (Department 4): head.accountant (Trưởng phòng), test.accountant (Nhân viên)
                    IF @HeadAccId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @HeadAccId AND [department_id] = 4)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@HeadAccId, 4, @SeedNow);
                    IF @AccId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @AccId AND [department_id] = 4)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@AccId, 4, @SeedNow);

                    -- Phòng Hành Chính - Pháp Chế (Department 5): head.legal (Trưởng phòng), test.legal (Nhân viên)
                    IF @HeadLegalId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @HeadLegalId AND [department_id] = 5)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@HeadLegalId, 5, @SeedNow);
                    IF @LegalId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @LegalId AND [department_id] = 5)
                        INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at]) VALUES (@LegalId, 5, @SeedNow);
                    """,
                    cancellationToken);
            }
            catch
            {
                // Ignore if running against a test/in-memory DB.
            }

            bool systemAdminExists =
                await _centralDbContext.SystemAdmins
                    .AnyAsync(
                        admin => admin.Username == normalizedUsername,
                        cancellationToken);

            if (systemAdminExists)
            {
                _logger.LogInformation(
                    "System Admin bootstrap skipped because account {Username} already exists.",
                    normalizedUsername);
                return;
            }

            var systemAdmin = new SystemAdmin
            {
                Username = normalizedUsername,
                FullName = _options.FullName.Trim(),
                Email = _options.Email.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                MustChangePassword = false,
                SessionVersion = 1
            };

            systemAdmin.PasswordHash =
                _passwordHasher.HashPassword(
                    systemAdmin,
                    _options.Password);

            _centralDbContext.SystemAdmins.Add(systemAdmin);
            _centralDbContext.Entry(systemAdmin).Property("EmployeeCode").CurrentValue = "ADM-SYSADMIN";

            await _centralDbContext.SaveChangesAsync(
                cancellationToken);

            try
            {
                await _centralDbContext.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE [EMPLOYEE]
                    SET [employee_personal_identifier] = '001099000001',
                        [employee_phone] = '0901000001'
                    WHERE [employee_username] = 'sysadmin'
                      AND ([employee_personal_identifier] IS NULL OR [employee_phone] IS NULL);
                    """,
                    cancellationToken);
            }
            catch
            {
            }

            _logger.LogWarning(
                "System Admin bootstrap created account {Username}. Disable SystemAdminBootstrap immediately.",
                normalizedUsername);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "System Admin bootstrap skipped due to database schema/seed constraint on {Username}.",
                normalizedUsername);
        }
    }
}
