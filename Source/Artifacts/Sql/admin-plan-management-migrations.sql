IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE TABLE [TenantDatabases] (
        [TenantDatabaseId] int NOT NULL IDENTITY,
        [DatabaseKey] varchar(100) NOT NULL,
        [DatabaseName] varchar(128) NOT NULL,
        [ConnectionString] nvarchar(2000) NOT NULL,
        [Mode] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantDatabases] PRIMARY KEY ([TenantDatabaseId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE TABLE [Tenants] (
        [TenantId] int NOT NULL IDENTITY,
        [TenantCode] varchar(50) NOT NULL,
        [TenantName] nvarchar(200) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [TenantDatabaseId] int NOT NULL,
        [ProvisioningError] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([TenantId]),
        CONSTRAINT [FK_Tenants_TenantDatabases_TenantDatabaseId] FOREIGN KEY ([TenantDatabaseId]) REFERENCES [TenantDatabases] ([TenantDatabaseId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantDatabases_DatabaseKey] ON [TenantDatabases] ([DatabaseKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantDatabases_DatabaseName] ON [TenantDatabases] ([DatabaseName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    CREATE INDEX [IX_Tenants_TenantDatabaseId] ON [Tenants] ([TenantDatabaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630051534_InitialCentral'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260630051534_InitialCentral', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260701092834_AddSystemAdmins'
)
BEGIN
    CREATE TABLE [SystemAdmins] (
        [SystemAdminId] int NOT NULL IDENTITY,
        [Username] varchar(100) NOT NULL,
        [PasswordHash] varchar(255) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Email] varchar(200) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemAdmins] PRIMARY KEY ([SystemAdminId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260701092834_AddSystemAdmins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemAdmins_Username] ON [SystemAdmins] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260701092834_AddSystemAdmins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260701092834_AddSystemAdmins', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812101953_Phase7_Slice02CentralSecurityAudit'
)
BEGIN
    CREATE TABLE [CentralSecurityAudits] (
        [CentralSecurityAuditId] bigint NOT NULL IDENTITY,
        [ActorSystemAdminId] int NULL,
        [TenantId] int NULL,
        [TenantCode] varchar(50) NULL,
        [Action] varchar(100) NOT NULL,
        [Result] varchar(30) NOT NULL,
        [FailureCode] varchar(64) NULL,
        [TargetType] varchar(50) NULL,
        [TargetId] varchar(100) NULL,
        [PreviousEmployeeType] tinyint NULL,
        [NewEmployeeType] tinyint NULL,
        [PreviousStatus] tinyint NULL,
        [NewStatus] tinyint NULL,
        [OccurredAt] datetime2 NOT NULL,
        [IpAddress] varchar(45) NULL,
        [UserAgent] nvarchar(1024) NULL,
        [CorrelationId] varchar(100) NOT NULL,
        CONSTRAINT [PK_CentralSecurityAudits] PRIMARY KEY ([CentralSecurityAuditId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812101953_Phase7_Slice02CentralSecurityAudit'
)
BEGIN
    CREATE INDEX [IX_CentralSecurityAudits_ActorSystemAdminId] ON [CentralSecurityAudits] ([ActorSystemAdminId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812101953_Phase7_Slice02CentralSecurityAudit'
)
BEGIN
    CREATE INDEX [IX_CentralSecurityAudits_OccurredAt] ON [CentralSecurityAudits] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812101953_Phase7_Slice02CentralSecurityAudit'
)
BEGIN
    CREATE INDEX [IX_CentralSecurityAudits_TenantId_OccurredAt] ON [CentralSecurityAudits] ([TenantId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812101953_Phase7_Slice02CentralSecurityAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812101953_Phase7_Slice02CentralSecurityAudit', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813085353_Phase7_Slice05CentralSecurityAuditAppendOnly'
)
BEGIN
    CREATE TRIGGER [dbo].[TR_CentralSecurityAudits_AppendOnly]
    ON [dbo].[CentralSecurityAudits]
    AFTER UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        THROW 51000, 'CentralSecurityAudits is append-only.', 1;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260813085353_Phase7_Slice05CentralSecurityAuditAppendOnly'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260813085353_Phase7_Slice05CentralSecurityAuditAppendOnly', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [PasswordChangedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [SessionVersion] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    ALTER TABLE [CentralSecurityAudits] ADD [ChangedFields] varchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902032457_AddSystemAdminAccountSecurity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902032457_AddSystemAdminAccountSecurity', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [AvatarContentType] varchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [AvatarFileSize] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [AvatarSha256] varchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [AvatarStorageKey] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [AvatarUpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [CoverContentType] varchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [CoverFileSize] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [CoverSha256] varchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [CoverStorageKey] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [CoverUpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902132707_AddSystemAdminProfileImages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902132707_AddSystemAdminProfileImages', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925122540_AllowMixedCaseTenantCodeAndLongName'
)
BEGIN
    DROP INDEX [IX_Tenants_TenantCode] ON [Tenants];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925122540_AllowMixedCaseTenantCodeAndLongName'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantName');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantName] nvarchar(500) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925122540_AllowMixedCaseTenantCodeAndLongName'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantCode');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantCode] varchar(50) COLLATE Latin1_General_100_CI_AS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925122540_AllowMixedCaseTenantCodeAndLongName'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925122540_AllowMixedCaseTenantCodeAndLongName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925122540_AllowMixedCaseTenantCodeAndLongName', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    ALTER TABLE [SystemAdmins] ADD [RecoveryEmailVerifiedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    CREATE TABLE [AccountRecoveryChallenges] (
        [RecoveryChallengeId] bigint NOT NULL IDENTITY,
        [PublicId] uniqueidentifier NOT NULL,
        [SystemAdminId] int NOT NULL,
        [Purpose] tinyint NOT NULL,
        [TokenHash] binary(32) NOT NULL,
        [EmailHash] binary(32) NOT NULL,
        [SessionVersionAtIssue] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [UsedAtUtc] datetime2 NULL,
        [RevokedAtUtc] datetime2 NULL,
        [LastRequestedAtUtc] datetime2 NOT NULL,
        [RequestCount] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AccountRecoveryChallenges] PRIMARY KEY ([RecoveryChallengeId]),
        CONSTRAINT [CK_AccountRecoveryChallenges_Account] CHECK ([SystemAdminId] > 0 AND [SessionVersionAtIssue] > 0 AND [RequestCount] > 0),
        CONSTRAINT [CK_AccountRecoveryChallenges_Dates] CHECK ([ExpiresAtUtc] > [CreatedAtUtc] AND [LastRequestedAtUtc] >= [CreatedAtUtc]),
        CONSTRAINT [CK_AccountRecoveryChallenges_Purpose] CHECK ([Purpose] IN (1, 2))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    CREATE TABLE [AccountRecoveryEmailOutbox] (
        [OutboxId] bigint NOT NULL IDENTITY,
        [RecoveryChallengeId] bigint NULL,
        [SystemAdminId] int NOT NULL,
        [TemplateKind] tinyint NOT NULL,
        [RecipientCiphertext] varbinary(1024) NULL,
        [TokenCiphertext] varbinary(512) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [Status] tinyint NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAtUtc] datetime2 NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntilUtc] datetime2 NULL,
        [SentAtUtc] datetime2 NULL,
        [FailedAtUtc] datetime2 NULL,
        [FailureCode] varchar(64) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AccountRecoveryEmailOutbox] PRIMARY KEY ([OutboxId]),
        CONSTRAINT [CK_AccountRecoveryEmailOutbox_Lease] CHECK (([Status] = 2 AND [LeaseId] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [LeaseId] IS NULL AND [LeaseUntilUtc] IS NULL)),
        CONSTRAINT [CK_AccountRecoveryEmailOutbox_Values] CHECK ([SystemAdminId] > 0 AND [TemplateKind] IN (1, 2, 3) AND [Status] IN (1, 2, 3, 4) AND [AttemptCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AccountRecoveryChallenges_PublicId] ON [AccountRecoveryChallenges] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    CREATE INDEX [IX_AccountRecoveryChallenges_SystemAdminId_CreatedAtUtc] ON [AccountRecoveryChallenges] ([SystemAdminId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AccountRecoveryChallenges_SystemAdminId_Purpose] ON [AccountRecoveryChallenges] ([SystemAdminId], [Purpose]) WHERE [UsedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AccountRecoveryEmailOutbox_RecoveryChallengeId] ON [AccountRecoveryEmailOutbox] ([RecoveryChallengeId]) WHERE [RecoveryChallengeId] IS NOT NULL AND [Status] IN (1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    CREATE INDEX [IX_AccountRecoveryEmailOutbox_Status_NextAttemptAtUtc] ON [AccountRecoveryEmailOutbox] ([Status], [NextAttemptAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002082056_AccountRecoveryPhase123'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002082056_AccountRecoveryPhase123', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [OwnerCustomerId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [CUSTOMER] (
        [customer_id] bigint NOT NULL IDENTITY,
        [customer_email] varchar(255) NOT NULL,
        [customer_phone] varchar(15) NULL,
        [customer_hand_phone] varchar(15) NULL,
        [customer_password_hash] varchar(255) NULL,
        [customer_display_name] nvarchar(100) NULL,
        [customer_type] varchar(20) NOT NULL DEFAULT 'CUSTOMER',
        [customer_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
        [customer_email_verified_at] datetime NULL,
        [customer_hand_phone_verified_at] datetime NULL,
        [customer_last_login_at] datetime NULL,
        [customer_dob] date NULL,
        [customer_address] nvarchar(500) NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_CUSTOMER] PRIMARY KEY ([customer_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [PROMOTION] (
        [promotion_id] bigint NOT NULL IDENTITY,
        [promotion_code] varchar(50) NOT NULL,
        [promotion_name] nvarchar(200) NOT NULL,
        [promotion_description] nvarchar(500) NULL,
        [promotion_discount_type] varchar(20) NOT NULL,
        [promotion_discount_value] decimal(19,4) NOT NULL,
        [promotion_max_discount_amount] decimal(19,4) NULL,
        [promotion_start_at] datetime NOT NULL,
        [promotion_end_at] datetime NULL,
        [promotion_usage_limit] int NULL,
        [promotion_usage_count] int NOT NULL DEFAULT 0,
        [promotion_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_PROMOTION] PRIMARY KEY ([promotion_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [SAAS_TYPE] (
        [saas_type_id] bigint NOT NULL IDENTITY,
        [saas_type_code] varchar(50) NOT NULL,
        [saas_type_name] nvarchar(150) NOT NULL,
        [saas_type_description] nvarchar(500) NULL,
        [saas_type_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_SAAS_TYPE] PRIMARY KEY ([saas_type_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [CUSTOMER_BUSINESS] (
        [customer_id] bigint NOT NULL,
        [customer_business_legal_name] nvarchar(1000) NOT NULL,
        [customer_business_company_tax_identifier] varchar(20) NOT NULL,
        [customer_business_phone] varchar(15) NULL,
        [customer_business_business_email] varchar(255) NULL,
        [customer_business_business_office_address] nvarchar(1000) NULL,
        [customer_business_business_head_office_address] nvarchar(1000) NULL,
        [customer_business_business_type] varchar(100) NULL,
        [customer_business_representative_name] nvarchar(255) NULL,
        [customer_business_operation_start_date] date NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_CUSTOMER_BUSINESS] PRIMARY KEY ([customer_id]),
        CONSTRAINT [FK_CUSTOMER_BUSINESS_CUSTOMER] FOREIGN KEY ([customer_id]) REFERENCES [CUSTOMER] ([customer_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [PLAN] (
        [plan_id] bigint NOT NULL IDENTITY,
        [saas_type_id] bigint NOT NULL,
        [plan_code] varchar(50) NOT NULL,
        [plan_name] nvarchar(150) NOT NULL,
        [plan_description] nvarchar(500) NULL,
        [plan_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
        [is_paid] bit NOT NULL,
        [allows_support_tickets] bit NOT NULL,
        [max_contracts] int NOT NULL DEFAULT 10,
        [max_scan_pages] int NOT NULL DEFAULT 20,
        [max_users] int NOT NULL DEFAULT 2,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_PLAN] PRIMARY KEY ([plan_id]),
        CONSTRAINT [FK_PLAN_SAAS_TYPE] FOREIGN KEY ([saas_type_id]) REFERENCES [SAAS_TYPE] ([saas_type_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [PLAN_VERSION] (
        [plan_version_id] bigint NOT NULL IDENTITY,
        [plan_id] bigint NOT NULL,
        [plan_version_version_number] int NOT NULL,
        [plan_version_purchase_type] varchar(20) NOT NULL DEFAULT 'BUYMANY',
        [plan_version_duration_days] int NULL,
        [plan_version_price] decimal(19,4) NOT NULL DEFAULT 0.0,
        [plan_version_currency] char(3) NOT NULL DEFAULT 'VND',
        [plan_version_billing_cycle] varchar(20) NOT NULL DEFAULT 'MONTHLY',
        [plan_version_effective_from] datetime NOT NULL DEFAULT (GETDATE()),
        [plan_version_effective_to] datetime NULL,
        [plan_version_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_PLAN_VERSION] PRIMARY KEY ([plan_version_id]),
        CONSTRAINT [FK_PLAN_VERSION_PLAN] FOREIGN KEY ([plan_id]) REFERENCES [PLAN] ([plan_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [SUBSCRIPTION] (
        [subscription_id] bigint NOT NULL IDENTITY,
        [tenant_id] int NULL,
        [customer_id] bigint NULL,
        [plan_version_id] bigint NOT NULL,
        [subscription_status] varchar(20) NOT NULL DEFAULT 'PENDING',
        [subscription_start_at] datetime NULL,
        [subscription_end_at] datetime NULL,
        [subscription_price_snapshot] decimal(19,4) NOT NULL DEFAULT 0.0,
        [subscription_currency] char(3) NOT NULL DEFAULT 'VND',
        [subscription_billing_cycle] varchar(20) NOT NULL DEFAULT 'MONTHLY',
        [subscription_auto_renew] bit NOT NULL,
        [is_paid] bit NOT NULL,
        [requested_tenant_code] varchar(50) NOT NULL,
        [requested_tenant_name] nvarchar(500) NOT NULL,
        [subscription_public_id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [provisioning_attempt_count] int NOT NULL DEFAULT 0,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_SUBSCRIPTION] PRIMARY KEY ([subscription_id]),
        CONSTRAINT [FK_SUBSCRIPTION_CUSTOMER] FOREIGN KEY ([customer_id]) REFERENCES [CUSTOMER] ([customer_id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SUBSCRIPTION_PLAN_VERSION] FOREIGN KEY ([plan_version_id]) REFERENCES [PLAN_VERSION] ([plan_version_id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SUBSCRIPTION_Tenants] FOREIGN KEY ([tenant_id]) REFERENCES [Tenants] ([TenantId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [SAAS_INVOICE] (
        [invoice_id] bigint NOT NULL IDENTITY,
        [invoice_code] varchar(100) NOT NULL,
        [customer_id] bigint NOT NULL,
        [subscription_id] bigint NULL,
        [invoice_billing_name] nvarchar(255) NOT NULL,
        [invoice_billing_tax_identifier] varchar(100) NULL,
        [invoice_billing_address] nvarchar(500) NULL,
        [invoice_billing_email] varchar(255) NULL,
        [invoice_billing_phone] varchar(30) NULL,
        [invoice_subtotal] decimal(19,4) NOT NULL,
        [invoice_discount_amount] decimal(19,4) NOT NULL DEFAULT 0.0,
        [invoice_total_amount] decimal(19,4) NOT NULL,
        [invoice_currency] char(3) NOT NULL DEFAULT 'VND',
        [invoice_status] varchar(30) NOT NULL DEFAULT 'DRAFT',
        [invoice_issued_at] datetime NULL,
        [invoice_due_at] datetime NULL,
        [invoice_paid_at] datetime NULL,
        [payos_order_code] bigint NULL,
        [payos_payment_link_id] varchar(100) NULL,
        [payos_checkout_url] nvarchar(1000) NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_SAAS_INVOICE] PRIMARY KEY ([invoice_id]),
        CONSTRAINT [FK_SAAS_INVOICE_CUSTOMER] FOREIGN KEY ([customer_id]) REFERENCES [CUSTOMER] ([customer_id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SAAS_INVOICE_SUBSCRIPTION] FOREIGN KEY ([subscription_id]) REFERENCES [SUBSCRIPTION] ([subscription_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [SUBSCRIPTION_PROMOTION_DETAIL] (
        [subscription_promotion_detail_id] bigint NOT NULL IDENTITY,
        [promotion_id] bigint NOT NULL,
        [subscription_id] bigint NOT NULL,
        [discount_type_applied] varchar(20) NOT NULL,
        [discount_value_applied] decimal(19,4) NOT NULL,
        [actual_money_deducted] decimal(19,4) NOT NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_SUBSCRIPTION_PROMOTION_DETAIL] PRIMARY KEY ([subscription_promotion_detail_id]),
        CONSTRAINT [FK_SUBSCRIPTION_PROMOTION_DETAIL_PROMOTION] FOREIGN KEY ([promotion_id]) REFERENCES [PROMOTION] ([promotion_id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SUBSCRIPTION_PROMOTION_DETAIL_SUBSCRIPTION] FOREIGN KEY ([subscription_id]) REFERENCES [SUBSCRIPTION] ([subscription_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [INVOICE_PROMOTION_DETAIL] (
        [invoice_promotion_detail_id] bigint NOT NULL IDENTITY,
        [invoice_id] bigint NOT NULL,
        [promotion_id] bigint NOT NULL,
        [discount_type_applied] varchar(20) NOT NULL,
        [discount_value_applied] decimal(19,4) NOT NULL,
        [actual_money_deducted] decimal(19,4) NOT NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_INVOICE_PROMOTION_DETAIL] PRIMARY KEY ([invoice_promotion_detail_id]),
        CONSTRAINT [FK_INVOICE_PROMOTION_DETAIL_INVOICE] FOREIGN KEY ([invoice_id]) REFERENCES [SAAS_INVOICE] ([invoice_id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_INVOICE_PROMOTION_DETAIL_PROMOTION] FOREIGN KEY ([promotion_id]) REFERENCES [PROMOTION] ([promotion_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE TABLE [SAAS_PAYMENT] (
        [payment_id] bigint NOT NULL IDENTITY,
        [invoice_id] bigint NOT NULL,
        [payment_reference] varchar(100) NOT NULL,
        [payment_method] varchar(30) NOT NULL,
        [payment_provider] varchar(100) NULL,
        [payment_provider_transaction_id] varchar(255) NULL,
        [payment_amount] decimal(19,4) NOT NULL,
        [payment_currency] char(3) NOT NULL DEFAULT 'VND',
        [payment_status] varchar(30) NOT NULL DEFAULT 'PENDING',
        [payment_paid_at] datetime NULL,
        [payment_failure_reason] varchar(500) NULL,
        [created_at] datetime NOT NULL DEFAULT (GETDATE()),
        [updated_at] datetime NULL,
        [created_by] bigint NULL,
        [updated_by] bigint NULL,
        CONSTRAINT [PK_SAAS_PAYMENT] PRIMARY KEY ([payment_id]),
        CONSTRAINT [FK_SAAS_PAYMENT_INVOICE] FOREIGN KEY ([invoice_id]) REFERENCES [SAAS_INVOICE] ([invoice_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'promotion_id', N'created_at', N'created_by', N'promotion_discount_type', N'promotion_discount_value', N'promotion_end_at', N'promotion_max_discount_amount', N'promotion_code', N'promotion_description', N'promotion_name', N'promotion_start_at', N'promotion_status', N'updated_at', N'updated_by', N'promotion_usage_limit') AND [object_id] = OBJECT_ID(N'[PROMOTION]'))
        SET IDENTITY_INSERT [PROMOTION] ON;
    EXEC(N'INSERT INTO [PROMOTION] ([promotion_id], [created_at], [created_by], [promotion_discount_type], [promotion_discount_value], [promotion_end_at], [promotion_max_discount_amount], [promotion_code], [promotion_description], [promotion_name], [promotion_start_at], [promotion_status], [updated_at], [updated_by], [promotion_usage_limit])
    VALUES (CAST(1 AS bigint), ''2026-01-01T00:00:00.000'', NULL, ''PERCENTAGE'', 10.0, NULL, 500000.0, ''TEST10'', NULL, N''Voucher test giảm 10%'', ''2026-01-01T00:00:00.000'', ''ACTIVE'', NULL, NULL, 100)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'promotion_id', N'created_at', N'created_by', N'promotion_discount_type', N'promotion_discount_value', N'promotion_end_at', N'promotion_max_discount_amount', N'promotion_code', N'promotion_description', N'promotion_name', N'promotion_start_at', N'promotion_status', N'updated_at', N'updated_by', N'promotion_usage_limit') AND [object_id] = OBJECT_ID(N'[PROMOTION]'))
        SET IDENTITY_INSERT [PROMOTION] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'saas_type_id', N'created_at', N'created_by', N'saas_type_code', N'saas_type_description', N'saas_type_name', N'saas_type_status', N'updated_at', N'updated_by') AND [object_id] = OBJECT_ID(N'[SAAS_TYPE]'))
        SET IDENTITY_INSERT [SAAS_TYPE] ON;
    EXEC(N'INSERT INTO [SAAS_TYPE] ([saas_type_id], [created_at], [created_by], [saas_type_code], [saas_type_description], [saas_type_name], [saas_type_status], [updated_at], [updated_by])
    VALUES (CAST(1 AS bigint), ''2026-01-01T00:00:00.000'', NULL, ''CONTRACK'', N''Phần mềm quản lý hợp đồng'', N''ConTrack'', ''ACTIVE'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'saas_type_id', N'created_at', N'created_by', N'saas_type_code', N'saas_type_description', N'saas_type_name', N'saas_type_status', N'updated_at', N'updated_by') AND [object_id] = OBJECT_ID(N'[SAAS_TYPE]'))
        SET IDENTITY_INSERT [SAAS_TYPE] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_id', N'allows_support_tickets', N'created_at', N'created_by', N'is_paid', N'max_contracts', N'max_scan_pages', N'max_users', N'plan_code', N'plan_description', N'plan_name', N'plan_status', N'saas_type_id', N'updated_at', N'updated_by') AND [object_id] = OBJECT_ID(N'[PLAN]'))
        SET IDENTITY_INSERT [PLAN] ON;
    EXEC(N'INSERT INTO [PLAN] ([plan_id], [allows_support_tickets], [created_at], [created_by], [is_paid], [max_contracts], [max_scan_pages], [max_users], [plan_code], [plan_description], [plan_name], [plan_status], [saas_type_id], [updated_at], [updated_by])
    VALUES (CAST(1 AS bigint), CAST(0 AS bit), ''2026-01-01T00:00:00.000'', NULL, CAST(0 AS bit), 10, 20, 2, ''TRIAL'', N''Trải nghiệm quy trình chính, không bao gồm Customer Support'', N''Dùng thử'', ''ACTIVE'', CAST(1 AS bigint), NULL, NULL),
    (CAST(2 AS bigint), CAST(1 AS bit), ''2026-01-01T00:00:00.000'', NULL, CAST(1 AS bit), 1000, 1000, 20, ''STANDARD'', N''Đầy đủ tính năng, có Customer Support'', N''Tiêu chuẩn'', ''ACTIVE'', CAST(1 AS bigint), NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_id', N'allows_support_tickets', N'created_at', N'created_by', N'is_paid', N'max_contracts', N'max_scan_pages', N'max_users', N'plan_code', N'plan_description', N'plan_name', N'plan_status', N'saas_type_id', N'updated_at', N'updated_by') AND [object_id] = OBJECT_ID(N'[PLAN]'))
        SET IDENTITY_INSERT [PLAN] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_version_id', N'plan_version_billing_cycle', N'created_at', N'created_by', N'plan_version_currency', N'plan_version_duration_days', N'plan_version_effective_from', N'plan_version_effective_to', N'plan_id', N'plan_version_purchase_type', N'plan_version_status', N'updated_at', N'updated_by', N'plan_version_version_number') AND [object_id] = OBJECT_ID(N'[PLAN_VERSION]'))
        SET IDENTITY_INSERT [PLAN_VERSION] ON;
    EXEC(N'INSERT INTO [PLAN_VERSION] ([plan_version_id], [plan_version_billing_cycle], [created_at], [created_by], [plan_version_currency], [plan_version_duration_days], [plan_version_effective_from], [plan_version_effective_to], [plan_id], [plan_version_purchase_type], [plan_version_status], [updated_at], [updated_by], [plan_version_version_number])
    VALUES (CAST(1 AS bigint), ''TRIAL'', ''2026-01-01T00:00:00.000'', NULL, ''VND'', 15, ''2026-01-01T00:00:00.000'', NULL, CAST(1 AS bigint), ''BUYMANY'', ''ACTIVE'', NULL, NULL, 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_version_id', N'plan_version_billing_cycle', N'created_at', N'created_by', N'plan_version_currency', N'plan_version_duration_days', N'plan_version_effective_from', N'plan_version_effective_to', N'plan_id', N'plan_version_purchase_type', N'plan_version_status', N'updated_at', N'updated_by', N'plan_version_version_number') AND [object_id] = OBJECT_ID(N'[PLAN_VERSION]'))
        SET IDENTITY_INSERT [PLAN_VERSION] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_version_id', N'plan_version_billing_cycle', N'created_at', N'created_by', N'plan_version_currency', N'plan_version_duration_days', N'plan_version_effective_from', N'plan_version_effective_to', N'plan_id', N'plan_version_price', N'plan_version_purchase_type', N'plan_version_status', N'updated_at', N'updated_by', N'plan_version_version_number') AND [object_id] = OBJECT_ID(N'[PLAN_VERSION]'))
        SET IDENTITY_INSERT [PLAN_VERSION] ON;
    EXEC(N'INSERT INTO [PLAN_VERSION] ([plan_version_id], [plan_version_billing_cycle], [created_at], [created_by], [plan_version_currency], [plan_version_duration_days], [plan_version_effective_from], [plan_version_effective_to], [plan_id], [plan_version_price], [plan_version_purchase_type], [plan_version_status], [updated_at], [updated_by], [plan_version_version_number])
    VALUES (CAST(2 AS bigint), ''QUARTERLY'', ''2026-01-01T00:00:00.000'', NULL, ''VND'', 90, ''2026-01-01T00:00:00.000'', NULL, CAST(2 AS bigint), 900000.0, ''BUYMANY'', ''ACTIVE'', NULL, NULL, 1),
    (CAST(3 AS bigint), ''SEMI_ANNUAL'', ''2026-01-01T00:00:00.000'', NULL, ''VND'', 180, ''2026-01-01T00:00:00.000'', NULL, CAST(2 AS bigint), 1620000.0, ''BUYMANY'', ''ACTIVE'', NULL, NULL, 2),
    (CAST(4 AS bigint), ''YEARLY'', ''2026-01-01T00:00:00.000'', NULL, ''VND'', 365, ''2026-01-01T00:00:00.000'', NULL, CAST(2 AS bigint), 2880000.0, ''BUYMANY'', ''ACTIVE'', NULL, NULL, 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'plan_version_id', N'plan_version_billing_cycle', N'created_at', N'created_by', N'plan_version_currency', N'plan_version_duration_days', N'plan_version_effective_from', N'plan_version_effective_to', N'plan_id', N'plan_version_price', N'plan_version_purchase_type', N'plan_version_status', N'updated_at', N'updated_by', N'plan_version_version_number') AND [object_id] = OBJECT_ID(N'[PLAN_VERSION]'))
        SET IDENTITY_INSERT [PLAN_VERSION] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_Tenants_OwnerCustomerId] ON [Tenants] ([OwnerCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CUSTOMER_customer_email] ON [CUSTOMER] ([customer_email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_INVOICE_PROMOTION_DETAIL_invoice_id] ON [INVOICE_PROMOTION_DETAIL] ([invoice_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_INVOICE_PROMOTION_DETAIL_promotion_id] ON [INVOICE_PROMOTION_DETAIL] ([promotion_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_PLAN_saas_type_id] ON [PLAN] ([saas_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PLAN_VERSION_Number] ON [PLAN_VERSION] ([plan_id], [plan_version_version_number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PROMOTION_code] ON [PROMOTION] ([promotion_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SAAS_INVOICE_customer_id] ON [SAAS_INVOICE] ([customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SAAS_INVOICE_subscription_id] ON [SAAS_INVOICE] ([subscription_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SAAS_INVOICE_code] ON [SAAS_INVOICE] ([invoice_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_SAAS_INVOICE_payos_order_code] ON [SAAS_INVOICE] ([payos_order_code]) WHERE [payos_order_code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SAAS_PAYMENT_invoice_id] ON [SAAS_PAYMENT] ([invoice_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SAAS_PAYMENT_ref] ON [SAAS_PAYMENT] ([payment_reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SAAS_TYPE_code] ON [SAAS_TYPE] ([saas_type_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_Customer] ON [SUBSCRIPTION] ([customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_plan_version_id] ON [SUBSCRIPTION] ([plan_version_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_RequestedTenantCode] ON [SUBSCRIPTION] ([requested_tenant_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_Status] ON [SUBSCRIPTION] ([subscription_status], [is_paid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_Tenant] ON [SUBSCRIPTION] ([tenant_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SUBSCRIPTION_public_id] ON [SUBSCRIPTION] ([subscription_public_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_PROMOTION_DETAIL_promotion_id] ON [SUBSCRIPTION_PROMOTION_DETAIL] ([promotion_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    CREATE INDEX [IX_SUBSCRIPTION_PROMOTION_DETAIL_subscription_id] ON [SUBSCRIPTION_PROMOTION_DETAIL] ([subscription_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD CONSTRAINT [FK_Tenants_CUSTOMER_OwnerCustomerId] FOREIGN KEY ([OwnerCustomerId]) REFERENCES [CUSTOMER] ([customer_id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027152534_AddSaasSubscription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027152534_AddSaasSubscription', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [SUBSCRIPTION] ADD [AllowsSupportTicketsSnapshot] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [SUBSCRIPTION] ADD [MaxContractsSnapshot] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [SUBSCRIPTION] ADD [MaxScanPagesSnapshot] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [SUBSCRIPTION] ADD [MaxUsersSnapshot] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [PLAN_VERSION] ADD [plan_version_duration_months] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    ALTER TABLE [PLAN] ADD [Revision] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    CREATE TABLE [PlanAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [PlanId] bigint NOT NULL,
        [ActorId] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [BeforeJson] nvarchar(max) NOT NULL,
        [AfterJson] nvarchar(max) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [IpAddress] nvarchar(max) NULL,
        [CorrelationId] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PlanAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN] SET [Revision] = 0
    WHERE [plan_id] = CAST(1 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN] SET [Revision] = 0
    WHERE [plan_id] = CAST(2 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN_VERSION] SET [plan_version_duration_months] = NULL
    WHERE [plan_version_id] = CAST(1 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN_VERSION] SET [plan_version_duration_months] = 3
    WHERE [plan_version_id] = CAST(2 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN_VERSION] SET [plan_version_duration_months] = 6
    WHERE [plan_version_id] = CAST(3 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    EXEC(N'UPDATE [PLAN_VERSION] SET [plan_version_duration_months] = 12
    WHERE [plan_version_id] = CAST(4 AS bigint);
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN

    UPDATE v SET plan_version_duration_months = CASE v.plan_version_duration_days WHEN 90 THEN 3 WHEN 180 THEN 6 WHEN 365 THEN 12 END
    FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1 AND v.plan_version_duration_months IS NULL;
    IF EXISTS(SELECT 1 FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1 AND v.plan_version_duration_months IS NULL)
     THROW 51000, 'Unmapped existing paid duration. Map duration_months explicitly before migration.', 1;
    UPDATE s SET MaxUsersSnapshot=p.max_users, MaxContractsSnapshot=p.max_contracts, MaxScanPagesSnapshot=p.max_scan_pages, AllowsSupportTicketsSnapshot=p.allows_support_tickets
    FROM SUBSCRIPTION s JOIN PLAN_VERSION v ON v.plan_version_id=s.plan_version_id JOIN [PLAN] p ON p.plan_id=v.plan_id;
    DECLARE @old bigint, @months int, @new bigint;
    DECLARE groups_cursor CURSOR LOCAL FAST_FORWARD FOR
     SELECT v.plan_id,v.plan_version_duration_months FROM PLAN_VERSION v JOIN [PLAN] p ON p.plan_id=v.plan_id WHERE p.is_paid=1
     GROUP BY v.plan_id,v.plan_version_duration_months HAVING v.plan_version_duration_months > (SELECT MIN(x.plan_version_duration_months) FROM PLAN_VERSION x WHERE x.plan_id=v.plan_id);
    OPEN groups_cursor; FETCH NEXT FROM groups_cursor INTO @old,@months;
    WHILE @@FETCH_STATUS=0 BEGIN
     INSERT INTO [PLAN](saas_type_id,plan_code,plan_name,plan_description,plan_status,is_paid,allows_support_tickets,max_contracts,max_scan_pages,max_users,created_at,Revision)
     SELECT saas_type_id,CONCAT(LEFT(plan_code,35),'_',@months,'M'),CONCAT(@months,N' tháng'),plan_description,plan_status,is_paid,allows_support_tickets,max_contracts,max_scan_pages,max_users,GETUTCDATE(),0 FROM [PLAN] WHERE plan_id=@old;
     SET @new=SCOPE_IDENTITY();
     UPDATE PLAN_VERSION SET plan_id=@new WHERE plan_id=@old AND plan_version_duration_months=@months;
     FETCH NEXT FROM groups_cursor INTO @old,@months;
    END
    CLOSE groups_cursor; DEALLOCATE groups_cursor;
    UPDATE p SET plan_name=CONCAT(v.months,N' tháng') FROM [PLAN] p CROSS APPLY(SELECT MIN(plan_version_duration_months) months FROM PLAN_VERSION WHERE plan_id=p.plan_id) v WHERE p.is_paid=1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PLAN_plan_code] ON [PLAN] ([plan_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028062558_AdminPlanManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028062558_AdminPlanManagement', N'10.0.0');
END;

COMMIT;
GO

