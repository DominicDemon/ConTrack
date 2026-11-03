-- ====================================================================================================
-- DỰ ÁN: CONTRACK - HỆ THỐNG NỀN TẢNG SAAS QUẢN LÝ & SỐ HÓA HỢP ĐỒNG TOÀN DIỆN
-- PHIÊN BẢN: 3.0 (CHUẨN HÓA 100% THEO SƠ ĐỒ ERD ẢNH & TƯƠNG THÍCH MÃ NGUỒN C# .NET EF CORE)
-- 
-- MỤC TIÊU KIẾN TRÚC:
-- 1. CENTRAL DATABASE (Nền tảng SaaS trung tâm - Chuẩn hóa theo 5 Sơ đồ ERD ảnh mẫu):
--    * 01 - IDENTITY & CUSTOMER: CUSTOMER, CUSTOMER_BUSINESS, AUDIT_LOG, TICKET_SUPPORT
--    * 02 - SAAS PLANS & TENANT: SAAS_TYPE, PLAN, PLAN_VERSION, SUBSCRIPTION, TENANT_DATABASES, TENANTS
--    * 03 - SALES, BILLING & PROMOTIONS: SAAS_INVOICE, SAAS_PAYMENT, PROMOTION
--    * 04 - EMPLOYEE & TICKET: TICKET_MESSAGE, TICKET_SUPPORT_EMPLOYEE, SystemAdmins, AccountRecovery
--    ★ RÀNG BUỘC NGHIỆP VỤ BẮT BUỘC: Trigger trg_CheckSubscriptionPaidForTicket
--      -> Khách hàng đăng ký gói Miễn phí (Free) không được tạo Ticket hỗ trợ kỹ thuật.
--      -> Chỉ gói Trả phí (Paid Plan) có Subscription hợp lệ và đã thanh toán mới được hỗ trợ.
-- 
-- 2. TENANT DATABASE (Quản trị Hợp đồng nội bộ Tenant - Chuẩn hóa 100% EF Core DbDtctechContext):
--    * [HƯỚNG 1]: Quản lý & Tạo hợp đồng online toàn diện (Soạn thảo mẫu, Biến động version,
--      Snapshot pháp lý bất biến, Phê duyệt nội bộ đa cấp, Cổng đối tác OTP & Đàm phán, Ký số,
--      Bàn giao, Nghiệm thu, Kế hoạch & Sổ cái thanh toán, Audit log & Thông báo).
--    * [HƯỚNG 2]: Số hóa hợp đồng bản cứng bằng Máy scan / App Mobile & OCR Bóc tách Key-Value
--      (Scan batch, Tự động xoay thẳng trang, OCR văn bản, Bóc tách Key, Tìm kiếm toàn văn và
--      Đồng bộ hóa 1-click sang Hợp đồng số).
-- ====================================================================================================

-- ####################################################################################################
-- PHẦN I: CENTRAL DATABASE (QUẢN TRỊ NỀN TẢNG SAAS, GÓI CƯỚC & TICKET THEO 100% ẢNH ERD)
-- Database đề xuất: ConTrack_CentralDB
-- ####################################################################################################

-- ----------------------------------------------------------------------------------------------------
-- 1.1. MULTI-TENANCY & HỆ THỐNG TRUNG TÂM (Khớp EF Core CentralDbContext)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantDatabases')
BEGIN
    CREATE TABLE TenantDatabases (
        TenantDatabaseId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantDatabases PRIMARY KEY,
        DatabaseKey VARCHAR(100) NOT NULL,
        DatabaseName VARCHAR(128) NOT NULL,
        ConnectionString NVARCHAR(2000) NOT NULL,
        Mode NVARCHAR(30) NOT NULL, -- Dedicated, Shared
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_TenantDatabases_DatabaseKey UNIQUE (DatabaseKey),
        CONSTRAINT UX_TenantDatabases_DatabaseName UNIQUE (DatabaseName)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE Tenants (
        TenantId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
        TenantCode VARCHAR(50) COLLATE Latin1_General_100_CI_AS NOT NULL,
        TenantName NVARCHAR(500) NOT NULL,
        TenantDatabaseId INT NOT NULL,
        Status NVARCHAR(30) NOT NULL, -- Active, Suspended, Provisioning, Pending
        ProvisioningError NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT UX_Tenants_TenantCode UNIQUE (TenantCode),
        CONSTRAINT FK_Tenants_TenantDatabases FOREIGN KEY (TenantDatabaseId) REFERENCES TenantDatabases(TenantDatabaseId)
    );
    CREATE NONCLUSTERED INDEX IX_Tenants_TenantDatabaseId ON Tenants(TenantDatabaseId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemAdmins')
BEGIN
    CREATE TABLE SystemAdmins (
        SystemAdminId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SystemAdmins PRIMARY KEY,
        Username VARCHAR(100) NOT NULL,
        PasswordHash VARCHAR(255) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        Email VARCHAR(200) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_SystemAdmins_IsActive DEFAULT 1,
        MustChangePassword BIT NOT NULL CONSTRAINT DF_SystemAdmins_MustChangePassword DEFAULT 0,
        SessionVersion INT NOT NULL CONSTRAINT DF_SystemAdmins_SessionVersion DEFAULT 1,
        PasswordChangedAt DATETIME2 NULL,
        RecoveryEmailVerifiedAt DATETIME2 NULL,
        AvatarStorageKey NVARCHAR(500) NULL,
        AvatarContentType VARCHAR(100) NULL,
        AvatarSha256 VARCHAR(64) NULL,
        AvatarFileSize BIGINT NULL,
        AvatarUpdatedAt DATETIME2 NULL,
        CoverStorageKey NVARCHAR(500) NULL,
        CoverContentType VARCHAR(100) NULL,
        CoverSha256 VARCHAR(64) NULL,
        CoverFileSize BIGINT NULL,
        CoverUpdatedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UX_SystemAdmins_Username UNIQUE (Username)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CentralSecurityAudits')
BEGIN
    CREATE TABLE CentralSecurityAudits (
        CentralSecurityAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CentralSecurityAudits PRIMARY KEY,
        TenantId INT NULL,
        TenantCode VARCHAR(50) NULL,
        ActorSystemAdminId INT NULL,
        Action VARCHAR(100) NOT NULL,
        TargetType VARCHAR(50) NULL,
        TargetId VARCHAR(100) NULL,
        PreviousStatus TINYINT NULL,
        NewStatus TINYINT NULL,
        PreviousEmployeeType TINYINT NULL,
        NewEmployeeType TINYINT NULL,
        ChangedFields VARCHAR(1000) NULL,
        Result VARCHAR(30) NOT NULL,
        FailureCode VARCHAR(64) NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IpAddress VARCHAR(45) NULL,
        UserAgent NVARCHAR(1024) NULL,
        CorrelationId VARCHAR(100) NOT NULL
    );
    CREATE NONCLUSTERED INDEX IX_CentralSecurityAudits_OccurredAt ON CentralSecurityAudits(OccurredAt);
    CREATE NONCLUSTERED INDEX IX_CentralSecurityAudits_Tenant_OccurredAt ON CentralSecurityAudits(TenantId, OccurredAt);
    CREATE NONCLUSTERED INDEX IX_CentralSecurityAudits_ActorAdmin ON CentralSecurityAudits(ActorSystemAdminId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountRecoveryChallenges')
BEGIN
    CREATE TABLE AccountRecoveryChallenges (
        RecoveryChallengeId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountRecoveryChallenges PRIMARY KEY,
        SystemAdminId INT NOT NULL,
        PublicId UNIQUEIDENTIFIER NOT NULL,
        TokenHash BINARY(32) NOT NULL,
        EmailHash BINARY(32) NOT NULL,
        Purpose TINYINT NOT NULL,
        SessionVersionAtIssue INT NOT NULL,
        RequestCount INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NOT NULL,
        LastRequestedAtUtc DATETIME2 NOT NULL,
        UsedAtUtc DATETIME2 NULL,
        RevokedAtUtc DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_AccountRecoveryChallenges_Account CHECK ([SystemAdminId] > 0 AND [SessionVersionAtIssue] > 0 AND [RequestCount] > 0),
        CONSTRAINT CK_AccountRecoveryChallenges_Dates CHECK ([ExpiresAtUtc] > [CreatedAtUtc] AND [LastRequestedAtUtc] >= [CreatedAtUtc]),
        CONSTRAINT CK_AccountRecoveryChallenges_Purpose CHECK ([Purpose] IN (1, 2)),
        CONSTRAINT UX_AccountRecoveryChallenges_PublicId UNIQUE (PublicId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountRecoveryEmailOutbox')
BEGIN
    CREATE TABLE AccountRecoveryEmailOutbox (
        OutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountRecoveryEmailOutbox PRIMARY KEY,
        SystemAdminId INT NOT NULL,
        RecoveryChallengeId BIGINT NULL,
        TemplateKind TINYINT NOT NULL,
        RecipientCiphertext VARBINARY(1024) NULL,
        TokenCiphertext VARBINARY(512) NULL,
        Status TINYINT NOT NULL,
        AttemptCount INT NOT NULL,
        NextAttemptAtUtc DATETIME2 NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NULL,
        SentAtUtc DATETIME2 NULL,
        FailedAtUtc DATETIME2 NULL,
        FailureCode VARCHAR(64) NULL,
        LeaseId UNIQUEIDENTIFIER NULL,
        LeaseUntilUtc DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 1.2. 01 - IDENTITY & CUSTOMER (Chuẩn 100% Ảnh ERD 1)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER')
BEGIN
    CREATE TABLE CUSTOMER (
        customer_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CUSTOMER PRIMARY KEY,
        customer_email VARCHAR(255) NOT NULL,
        customer_phone VARCHAR(15) NULL,
        customer_hand_phone VARCHAR(15) NULL,
        customer_password_hash VARCHAR(255) NOT NULL,
        customer_display_name VARCHAR(100) NULL,
        customer_type VARCHAR(20) NOT NULL CONSTRAINT DF_CUSTOMER_type DEFAULT 'CUSTOMER',
        customer_status VARCHAR(20) NOT NULL CONSTRAINT DF_CUSTOMER_status DEFAULT 'ACTIVE',
        customer_email_verified_at DATETIME NULL,
        customer_hand_phone_verified_at DATETIME NULL,
        customer_last_login_at DATETIME NULL,
        customer_dob DATE NULL,
        customer_address NVARCHAR(500) NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_CUSTOMER_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT UX_CUSTOMER_customer_email UNIQUE (customer_email)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_BUSINESS')
BEGIN
    CREATE TABLE CUSTOMER_BUSINESS (
        customer_id BIGINT NOT NULL CONSTRAINT PK_CUSTOMER_BUSINESS PRIMARY KEY,
        customer_business_legal_name NVARCHAR(1000) NOT NULL,
        customer_business_company_tax_identifier VARCHAR(20) NOT NULL,
        customer_business_phone VARCHAR(15) NULL,
        customer_business_business_email VARCHAR(255) NULL,
        customer_business_business_office_address NVARCHAR(1000) NULL,
        customer_business_business_head_office_address NVARCHAR(1000) NULL,
        customer_business_business_type VARCHAR(100) NULL,
        customer_business_representative_name NVARCHAR(255) NULL,
        customer_business_operation_start_date DATE NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_CUSTOMER_BUSINESS_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT FK_CUSTOMER_BUSINESS_CUSTOMER FOREIGN KEY (customer_id) REFERENCES CUSTOMER(customer_id) ON DELETE CASCADE
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 1.3. 02 - SAAS PLANS & TENANT (Chuẩn 100% Ảnh ERD 2 & Mở rộng Nghiệp vụ Hợp đồng)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SAAS_TYPE')
BEGIN
    CREATE TABLE SAAS_TYPE (
        saas_type_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SAAS_TYPE PRIMARY KEY,
        saas_type_code VARCHAR(50) NOT NULL,
        saas_type_name NVARCHAR(150) NOT NULL,
        saas_type_description NVARCHAR(500) NULL,
        saas_type_status VARCHAR(20) NOT NULL CONSTRAINT DF_SAAS_TYPE_status DEFAULT 'ACTIVE',
        created_at DATETIME NOT NULL CONSTRAINT DF_SAAS_TYPE_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT UX_SAAS_TYPE_code UNIQUE (saas_type_code)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PLAN')
BEGIN
    CREATE TABLE PLAN (
        plan_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PLAN PRIMARY KEY,
        saas_type_id BIGINT NOT NULL,
        plan_code VARCHAR(50) NOT NULL,
        plan_name NVARCHAR(150) NOT NULL,
        plan_description NVARCHAR(500) NULL,
        plan_status VARCHAR(20) NOT NULL CONSTRAINT DF_PLAN_status DEFAULT 'ACTIVE',
        is_paid BIT NOT NULL CONSTRAINT DF_PLAN_is_paid DEFAULT 0, -- 0=Gói Free, 1=Gói Trả phí
        allows_support_tickets BIT NOT NULL CONSTRAINT DF_PLAN_allows_tickets DEFAULT 0, -- Gói Free=0, Paid=1
        max_contracts INT NOT NULL CONSTRAINT DF_PLAN_max_contracts DEFAULT 10,
        max_scan_pages INT NOT NULL CONSTRAINT DF_PLAN_max_scan DEFAULT 20,
        max_users INT NOT NULL CONSTRAINT DF_PLAN_max_users DEFAULT 2,
        created_at DATETIME NOT NULL CONSTRAINT DF_PLAN_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT FK_PLAN_SAAS_TYPE FOREIGN KEY (saas_type_id) REFERENCES SAAS_TYPE(saas_type_id)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PLAN_VERSION')
BEGIN
    CREATE TABLE PLAN_VERSION (
        plan_version_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PLAN_VERSION PRIMARY KEY,
        plan_id BIGINT NOT NULL,
        plan_version_version_number INT NOT NULL,
        plan_version_purchase_type VARCHAR(20) NOT NULL CONSTRAINT DF_PLAN_VERSION_type DEFAULT 'BUYMANY',
        plan_version_duration_days INT NULL,
        plan_version_price DECIMAL(19,4) NOT NULL DEFAULT 0,
        plan_version_currency CHAR(3) NOT NULL CONSTRAINT DF_PLAN_VERSION_currency DEFAULT 'VND',
        plan_version_billing_cycle VARCHAR(20) NOT NULL CONSTRAINT DF_PLAN_VERSION_cycle DEFAULT 'MONTHLY',
        plan_version_effective_from DATETIME NOT NULL CONSTRAINT DF_PLAN_VERSION_from DEFAULT GETDATE(),
        plan_version_effective_to DATETIME NULL,
        plan_version_status VARCHAR(20) NOT NULL CONSTRAINT DF_PLAN_VERSION_status DEFAULT 'ACTIVE',
        created_at DATETIME NOT NULL CONSTRAINT DF_PLAN_VERSION_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT FK_PLAN_VERSION_PLAN FOREIGN KEY (plan_id) REFERENCES PLAN(plan_id),
        CONSTRAINT UX_PLAN_VERSION_Number UNIQUE (plan_id, plan_version_version_number)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SUBSCRIPTION')
BEGIN
    CREATE TABLE SUBSCRIPTION (
        subscription_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SUBSCRIPTION PRIMARY KEY,
        tenant_id INT NOT NULL,                     -- Liên kết với Tenant trong ConTrack
        customer_id BIGINT NULL,                    -- Liên kết với Khách hàng mua gói
        plan_version_id BIGINT NOT NULL,
        subscription_status VARCHAR(20) NOT NULL CONSTRAINT DF_SUBSCRIPTION_status DEFAULT 'PENDING', -- PENDING, ACTIVE, EXPIRED, SUSPENDED, CANCELLED
        subscription_start_at DATETIME NULL,
        subscription_end_at DATETIME NULL,
        subscription_price_snapshot DECIMAL(19,4) NOT NULL DEFAULT 0,
        subscription_currency CHAR(3) NOT NULL CONSTRAINT DF_SUBSCRIPTION_currency DEFAULT 'VND',
        subscription_billing_cycle VARCHAR(20) NOT NULL CONSTRAINT DF_SUBSCRIPTION_cycle DEFAULT 'MONTHLY',
        subscription_auto_renew BIT NOT NULL CONSTRAINT DF_SUBSCRIPTION_renew DEFAULT 0,
        is_paid BIT NOT NULL CONSTRAINT DF_SUBSCRIPTION_is_paid DEFAULT 0, -- Cờ xác định trạng thái thanh toán
        created_at DATETIME NOT NULL CONSTRAINT DF_SUBSCRIPTION_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT FK_SUBSCRIPTION_Tenants FOREIGN KEY (tenant_id) REFERENCES Tenants(TenantId),
        CONSTRAINT FK_SUBSCRIPTION_CUSTOMER FOREIGN KEY (customer_id) REFERENCES CUSTOMER(customer_id),
        CONSTRAINT FK_SUBSCRIPTION_PLAN_VERSION FOREIGN KEY (plan_version_id) REFERENCES PLAN_VERSION(plan_version_id)
    );
    CREATE NONCLUSTERED INDEX IX_SUBSCRIPTION_Tenant ON SUBSCRIPTION(tenant_id);
    CREATE NONCLUSTERED INDEX IX_SUBSCRIPTION_Customer ON SUBSCRIPTION(customer_id);
    CREATE NONCLUSTERED INDEX IX_SUBSCRIPTION_Status ON SUBSCRIPTION(subscription_status, is_paid);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 1.4. 03 - SALES, BILLING & PROMOTIONS CỦA GÓI CƯỚC SAAS (Chuẩn 100% Ảnh ERD 3)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PROMOTION')
BEGIN
    CREATE TABLE PROMOTION (
        promotion_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PROMOTION PRIMARY KEY,
        promotion_code VARCHAR(50) NOT NULL,
        promotion_name NVARCHAR(200) NOT NULL,
        promotion_description NVARCHAR(500) NULL,
        promotion_discount_type VARCHAR(20) NOT NULL, -- PERCENTAGE, FIXED_AMOUNT
        promotion_discount_value DECIMAL(19,4) NOT NULL,
        promotion_max_discount_amount DECIMAL(19,4) NULL,
        promotion_start_at DATETIME NOT NULL,
        promotion_end_at DATETIME NULL,
        promotion_usage_limit INT NULL,
        promotion_usage_count INT NOT NULL CONSTRAINT DF_PROMOTION_usage DEFAULT 0,
        promotion_status VARCHAR(20) NOT NULL CONSTRAINT DF_PROMOTION_status DEFAULT 'ACTIVE',
        created_at DATETIME NOT NULL CONSTRAINT DF_PROMOTION_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT UX_PROMOTION_code UNIQUE (promotion_code)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SAAS_INVOICE')
BEGIN
    CREATE TABLE SAAS_INVOICE (
        invoice_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SAAS_INVOICE PRIMARY KEY,
        invoice_code VARCHAR(100) NOT NULL,
        customer_id BIGINT NOT NULL,
        subscription_id BIGINT NULL,
        invoice_billing_name NVARCHAR(255) NOT NULL,
        invoice_billing_tax_identifier VARCHAR(100) NULL,
        invoice_billing_address NVARCHAR(500) NULL,
        invoice_billing_email VARCHAR(255) NULL,
        invoice_billing_phone VARCHAR(30) NULL,
        invoice_subtotal DECIMAL(19,4) NOT NULL,
        invoice_discount_amount DECIMAL(19,4) NOT NULL CONSTRAINT DF_SAAS_INVOICE_discount DEFAULT 0,
        invoice_total_amount DECIMAL(19,4) NOT NULL,
        invoice_currency CHAR(3) NOT NULL CONSTRAINT DF_SAAS_INVOICE_currency DEFAULT 'VND',
        invoice_status VARCHAR(30) NOT NULL CONSTRAINT DF_SAAS_INVOICE_status DEFAULT 'DRAFT',
        invoice_issued_at DATETIME NULL,
        invoice_due_at DATETIME NULL,
        invoice_paid_at DATETIME NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_SAAS_INVOICE_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT UX_SAAS_INVOICE_code UNIQUE (invoice_code),
        CONSTRAINT FK_SAAS_INVOICE_CUSTOMER FOREIGN KEY (customer_id) REFERENCES CUSTOMER(customer_id),
        CONSTRAINT FK_SAAS_INVOICE_SUBSCRIPTION FOREIGN KEY (subscription_id) REFERENCES SUBSCRIPTION(subscription_id)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SAAS_PAYMENT')
BEGIN
    CREATE TABLE SAAS_PAYMENT (
        payment_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SAAS_PAYMENT PRIMARY KEY,
        invoice_id BIGINT NOT NULL,
        payment_reference VARCHAR(100) NOT NULL,
        payment_method VARCHAR(30) NOT NULL,
        payment_provider VARCHAR(100) NULL,
        payment_provider_transaction_id VARCHAR(255) NULL,
        payment_amount DECIMAL(19,4) NOT NULL,
        payment_currency CHAR(3) NOT NULL CONSTRAINT DF_SAAS_PAYMENT_currency DEFAULT 'VND',
        payment_status VARCHAR(30) NOT NULL CONSTRAINT DF_SAAS_PAYMENT_status DEFAULT 'PENDING',
        payment_paid_at DATETIME NULL,
        payment_failure_reason VARCHAR(500) NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_SAAS_PAYMENT_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        CONSTRAINT UX_SAAS_PAYMENT_ref UNIQUE (payment_reference),
        CONSTRAINT FK_SAAS_PAYMENT_INVOICE FOREIGN KEY (invoice_id) REFERENCES SAAS_INVOICE(invoice_id)
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 1.5. 04 - EMPLOYEE & TICKET SUPPORT (Chuẩn 100% Ảnh ERD 1 & Ảnh ERD 5)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_SUPPORT')
BEGIN
    CREATE TABLE TICKET_SUPPORT (
        ticket_support_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TICKET_SUPPORT PRIMARY KEY,
        customer_id BIGINT NULL,                    -- Khách hàng tạo ticket
        tenant_id INT NULL,                         -- Thuộc Tenant nào trong ConTrack
        ticket_code VARCHAR(30) NOT NULL,           -- Vd: TK-202610-001
        ticket_subject NVARCHAR(255) NOT NULL,
        ticket_description NVARCHAR(MAX) NOT NULL,
        ticket_priority VARCHAR(20) NOT NULL CONSTRAINT DF_TICKET_priority DEFAULT 'Medium', -- Low, Medium, High, Urgent
        ticket_status VARCHAR(30) NOT NULL CONSTRAINT DF_TICKET_status DEFAULT 'Open',       -- Open, In_Progress, Pending_Customer, Resolved, Closed
        ticket_resolution NVARCHAR(MAX) NULL,       -- Phương án/kết quả giải quyết
        ticket_resolved_at DATETIME NULL,
        ticket_closed_at DATETIME NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_TICKET_created_at DEFAULT GETDATE(),
        updated_at DATETIME NULL,
        CONSTRAINT UX_TICKET_SUPPORT_code UNIQUE (ticket_code),
        CONSTRAINT FK_TICKET_SUPPORT_CUSTOMER FOREIGN KEY (customer_id) REFERENCES CUSTOMER(customer_id),
        CONSTRAINT FK_TICKET_SUPPORT_Tenants FOREIGN KEY (tenant_id) REFERENCES Tenants(TenantId)
    );
    CREATE NONCLUSTERED INDEX IX_TICKET_SUPPORT_Customer ON TICKET_SUPPORT(customer_id);
    CREATE NONCLUSTERED INDEX IX_TICKET_SUPPORT_Tenant ON TICKET_SUPPORT(tenant_id);
    CREATE NONCLUSTERED INDEX IX_TICKET_SUPPORT_Status ON TICKET_SUPPORT(ticket_status);
END;
GO

-- Trigger Nghiệp vụ cốt lõi: Chặn gói Miễn phí (Free) tạo Ticket hỗ trợ kỹ thuật
CREATE OR ALTER TRIGGER trg_CheckSubscriptionPaidForTicket
ON TICKET_SUPPORT
FOR INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Kiểm tra điều kiện thuê bao của Tenant / Customer tạo Ticket
    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN SUBSCRIPTION s ON (i.tenant_id = s.tenant_id OR i.customer_id = s.customer_id)
        LEFT JOIN PLAN_VERSION pv ON s.plan_version_id = pv.plan_version_id
        LEFT JOIN PLAN p ON pv.plan_id = p.plan_id
        WHERE s.subscription_id IS NULL 
           OR s.subscription_status <> 'ACTIVE'
           OR s.is_paid = 0
           OR p.allows_support_tickets = 0
           OR p.is_paid = 0
    )
    BEGIN
        RAISERROR (N'Chính sách ConTrack: Khách hàng sử dụng gói Miễn phí (Free) hoặc chưa thanh toán thuê bao không được quyền mở Ticket hỗ trợ kỹ thuật!', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_MESSAGE')
BEGIN
    CREATE TABLE TICKET_MESSAGE (
        ticket_message_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TICKET_MESSAGE PRIMARY KEY,
        ticket_support_id BIGINT NOT NULL,
        sender_customer_id BIGINT NULL,             -- Nếu khách hàng gửi (FK to CUSTOMER)
        sender_employee_id INT NULL,                -- Nếu nhân viên kỹ thuật gửi (FK to SystemAdmins)
        ticket_message_content NVARCHAR(MAX) NOT NULL,
        ticket_message_type VARCHAR(20) NOT NULL CONSTRAINT DF_TICKET_MESSAGE_type DEFAULT 'TEXT',
        ticket_message_attachment_url NVARCHAR(1000) NULL,
        ticket_message_attachment_name NVARCHAR(255) NULL,
        ticket_message_attachment_type VARCHAR(100) NULL,
        created_at DATETIME NOT NULL CONSTRAINT DF_TICKET_MESSAGE_created_at DEFAULT GETDATE(),
        CONSTRAINT FK_TICKET_MESSAGE_TICKET FOREIGN KEY (ticket_support_id) REFERENCES TICKET_SUPPORT(ticket_support_id) ON DELETE CASCADE,
        CONSTRAINT FK_TICKET_MESSAGE_CUSTOMER FOREIGN KEY (sender_customer_id) REFERENCES CUSTOMER(customer_id),
        CONSTRAINT FK_TICKET_MESSAGE_EMPLOYEE FOREIGN KEY (sender_employee_id) REFERENCES SystemAdmins(SystemAdminId)
    );
    CREATE NONCLUSTERED INDEX IX_TICKET_MESSAGE_Ticket_Created ON TICKET_MESSAGE(ticket_support_id, created_at);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TICKET_SUPPORT_EMPLOYEE')
BEGIN
    CREATE TABLE TICKET_SUPPORT_EMPLOYEE (
        ticket_support_employee_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TICKET_SUPPORT_EMPLOYEE PRIMARY KEY,
        ticket_support_id BIGINT NOT NULL,
        employee_id INT NOT NULL,                   -- Nhân viên phụ trách hỗ trợ (FK to SystemAdmins)
        tme_assignment_role VARCHAR(20) NOT NULL CONSTRAINT DF_TSE_role DEFAULT 'PRIMARY', -- PRIMARY, SECONDARY, ESCALATED
        tme_assigned_at DATETIME NOT NULL CONSTRAINT DF_TSE_assigned_at DEFAULT GETDATE(),
        tme_unassigned_at DATETIME NULL,
        tme_assigned_by_employee_id INT NULL,
        CONSTRAINT FK_TSE_TICKET FOREIGN KEY (ticket_support_id) REFERENCES TICKET_SUPPORT(ticket_support_id) ON DELETE CASCADE,
        CONSTRAINT FK_TSE_EMPLOYEE FOREIGN KEY (employee_id) REFERENCES SystemAdmins(SystemAdminId)
    );
    CREATE NONCLUSTERED INDEX IX_TSE_Ticket ON TICKET_SUPPORT_EMPLOYEE(ticket_support_id);
    CREATE NONCLUSTERED INDEX IX_TSE_Employee ON TICKET_SUPPORT_EMPLOYEE(employee_id);
END;
GO


-- ####################################################################################################
-- PHẦN II: TENANT DATABASE (QUẢN TRỊ NỘI BỘ TENANT - HỆ THỐNG QUẢN LÝ & SỐ HÓA HỢP ĐỒNG CONTRACK)
-- Database đề xuất: ConTrack_TenantDB_DtcTech
-- ####################################################################################################

-- ----------------------------------------------------------------------------------------------------
-- 2.1. TỔ CHỨC & PHÂN QUYỀN NỘI BỘ TENANT (Trùng khớp 100% DbDtctechContext)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Department')
BEGIN
    CREATE TABLE tbl_Department (
        DepartmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Department_DepartmentID PRIMARY KEY,
        DepartmentCode VARCHAR(20) NULL,
        DepartmentName NVARCHAR(200) NULL,
        ModifiedDate DATETIME NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Employee')
BEGIN
    CREATE TABLE tbl_Employee (
        EmployeeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Employee PRIMARY KEY,
        EmployeeAccount VARCHAR(50) NULL,
        EmployeePassword VARCHAR(255) NULL,
        EmployeeFullName NVARCHAR(100) NULL,
        EmployeeCode VARCHAR(30) NULL,
        EmployeeBirthDate DATETIME NULL,
        EmployeeAddress NVARCHAR(500) NULL,
        EmployeePhone VARCHAR(15) NULL,
        EmployeeMobile VARCHAR(15) NULL,
        EmployeeEmail VARCHAR(100) NULL,
        EmployeeImageIcon VARCHAR(50) NULL,
        DepartmentID INT NULL,
        DateCreated DATETIME NULL DEFAULT GETDATE(),
        DateModified DATETIME NULL,
        Gender NCHAR(1) NULL,
        UserRoles VARCHAR(20) NULL,
        HireDate DATETIME NULL,
        MaritalStatus NCHAR(1) NULL,
        Others NVARCHAR(2000) NULL,
        DefaultPage VARCHAR(200) NULL,
        MustChangePassword BIT NOT NULL DEFAULT 0,
        SessionVersion INT NOT NULL DEFAULT 1,
        PasswordChangedAt DATETIME2 NULL,
        AvatarStorageKey NVARCHAR(500) NULL,
        AvatarContentType VARCHAR(100) NULL,
        AvatarSha256 VARCHAR(64) NULL,
        AvatarUpdatedAt DATETIME2 NULL,
        CoverStorageKey NVARCHAR(500) NULL,
        CoverContentType VARCHAR(100) NULL,
        CoverSha256 VARCHAR(64) NULL,
        CoverUpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_tbl_Employee_tbl_Department FOREIGN KEY (DepartmentID) REFERENCES tbl_Department(DepartmentID)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_AuthorizationAudit')
BEGIN
    CREATE TABLE tbl_AuthorizationAudit (
        AuthorizationAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_AuthorizationAudit PRIMARY KEY,
        TenantId INT NOT NULL,
        ActorEmployeeId INT NULL,
        ActorType VARCHAR(30) NOT NULL,
        Action VARCHAR(100) NOT NULL,
        TargetType VARCHAR(50) NOT NULL,
        TargetId VARCHAR(100) NOT NULL,
        ChangedFields VARCHAR(1000) NULL,
        Result VARCHAR(30) NOT NULL,
        FailureCode VARCHAR(64) NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IpAddress VARCHAR(45) NULL,
        UserAgent NVARCHAR(1024) NULL,
        CorrelationId VARCHAR(100) NOT NULL
    );
    CREATE NONCLUSTERED INDEX IX_tbl_AuthorizationAudit_Tenant_Occurred ON tbl_AuthorizationAudit(TenantId, OccurredAt);
    CREATE NONCLUSTERED INDEX IX_tbl_AuthorizationAudit_Actor ON tbl_AuthorizationAudit(ActorEmployeeId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_AccountRecoveryChallenge')
BEGIN
    CREATE TABLE tbl_AccountRecoveryChallenge (
        RecoveryChallengeId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_AccountRecoveryChallenge PRIMARY KEY,
        EmployeeId INT NOT NULL,
        PublicId UNIQUEIDENTIFIER NOT NULL,
        TokenHash BINARY(32) NOT NULL,
        EmailHash BINARY(32) NOT NULL,
        Purpose TINYINT NOT NULL,
        SessionVersionAtIssue INT NOT NULL,
        RequestCount INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NOT NULL,
        LastRequestedAtUtc DATETIME2 NOT NULL,
        UsedAtUtc DATETIME2 NULL,
        RevokedAtUtc DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_AccountRecoveryEmailOutbox')
BEGIN
    CREATE TABLE tbl_AccountRecoveryEmailOutbox (
        OutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_AccountRecoveryEmailOutbox PRIMARY KEY,
        EmployeeId INT NOT NULL,
        RecoveryChallengeId BIGINT NULL,
        TemplateKind TINYINT NOT NULL,
        RecipientCiphertext VARBINARY(1024) NULL,
        TokenCiphertext VARBINARY(512) NULL,
        Status TINYINT NOT NULL,
        AttemptCount INT NOT NULL,
        NextAttemptAtUtc DATETIME2 NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        ExpiresAtUtc DATETIME2 NULL,
        SentAtUtc DATETIME2 NULL,
        FailedAtUtc DATETIME2 NULL,
        FailureCode VARCHAR(64) NULL,
        LeaseId UNIQUEIDENTIFIER NULL,
        LeaseUntilUtc DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.2. HỒ SƠ PHÁP LÝ & DANH MỤC THƯƠNG MẠI (DANH TỪ SỐ NHIỀU tbl_Categories, tbl_Products, tbl_Services...)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_TenantLegalProfile')
BEGIN
    CREATE TABLE tbl_TenantLegalProfile (
        TenantLegalProfileId INT NOT NULL CONSTRAINT PK_tbl_TenantLegalProfile PRIMARY KEY,
        LegalEntityName NVARCHAR(500) NOT NULL,
        LegalEntityNameEn NVARCHAR(500) NULL,
        TaxCode VARCHAR(50) NOT NULL,
        Address NVARCHAR(2000) NOT NULL,
        AddressEn NVARCHAR(2000) NULL,
        RepresentativeName NVARCHAR(200) NOT NULL,
        RepresentativeTitle NVARCHAR(200) NOT NULL,
        RepresentativeTitleEn NVARCHAR(200) NULL,
        PhoneNumber VARCHAR(30) NOT NULL,
        FaxNumber VARCHAR(30) NULL,
        BankAccountNumber VARCHAR(100) NOT NULL,
        BankName NVARCHAR(500) NOT NULL,
        BankNameEn NVARCHAR(500) NULL,
        CreatedByEmployeeId INT NOT NULL,
        UpdatedByEmployeeId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_TenantLegalProfile_Singleton CHECK ([TenantLegalProfileId] = 1),
        CONSTRAINT CK_tbl_TenantLegalProfile_Employees CHECK ([CreatedByEmployeeId] > 0 AND [UpdatedByEmployeeId] > 0)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Customers')
BEGIN
    CREATE TABLE tbl_Customers (
        CustomerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Customers PRIMARY KEY,
        CustomerCode VARCHAR(30) NULL,
        CustomerAccount VARCHAR(50) NULL,
        CustomerPassword VARCHAR(50) NULL,
        CustomerFullName NVARCHAR(100) NULL,
        CustomerCompany NVARCHAR(1000) NULL,
        CustomerCompanyEn NVARCHAR(500) NULL,
        CustomerAddress NVARCHAR(2000) NULL,
        CustomerAddressEn NVARCHAR(500) NULL,
        CustomerPhone VARCHAR(15) NULL,
        CustomerMobile VARCHAR(15) NULL,
        CustomerFaxNumber VARCHAR(15) NULL,
        CustomerEmail VARCHAR(50) NULL,
        CustomerWebsite NVARCHAR(500) NULL,
        CustomerTaxCode VARCHAR(30) NULL,
        CustomerCity NVARCHAR(1000) NULL,
        CustomerCityEn NVARCHAR(100) NULL,
        CustomerCountry NVARCHAR(200) NULL,
        CustomerCountryEn NVARCHAR(100) NULL,
        CustomerZipCode VARCHAR(15) NULL,
        CustomerImageIcon VARCHAR(50) NULL,
        CustomerRepresentativeName NVARCHAR(200) NULL,
        CustomerRepresentativeTitle NVARCHAR(200) NULL,
        CustomerRepresentativeTitleEn NVARCHAR(200) NULL,
        CustomerContactPersonName NVARCHAR(200) NULL,
        CustomerContactPersonPhone VARCHAR(20) NULL,
        CustomerContactPersonTitle NVARCHAR(200) NULL,
        CustomerContactPersonTitleEn NVARCHAR(200) NULL,
        CustomerBankAccountNumber VARCHAR(100) NULL,
        CustomerBankName NVARCHAR(500) NULL,
        CustomerBankNameEn NVARCHAR(500) NULL,
        CustomerComments NVARCHAR(2000) NULL,
        CustomerNotes NVARCHAR(2000) NULL,
        CustomerBirthday DATETIME NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_Customers_Status DEFAULT 0,
        DateCreated DATETIME NULL CONSTRAINT DF_tbl_Customers_DateCreated DEFAULT GETDATE(),
        DateModified DATETIME NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_CustomerInteraction')
BEGIN
    CREATE TABLE tbl_CustomerInteraction (
        InteractionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_CustomerInteraction PRIMARY KEY,
        CustomerId INT NOT NULL,
        EmployeeId INT NOT NULL,
        InteractionDate DATETIME NOT NULL DEFAULT GETDATE(),
        InteractionType VARCHAR(50) NOT NULL,
        InteractionSubject NVARCHAR(1000) NOT NULL,
        InteractionContent NVARCHAR(MAX) NULL,
        NextFollowUpDate DATETIME NULL,
        CONSTRAINT FK_tbl_CustomerInteraction_tbl_Customers FOREIGN KEY (CustomerId) REFERENCES tbl_Customers(CustomerId),
        CONSTRAINT FK_tbl_CustomerInteraction_tbl_Employee FOREIGN KEY (EmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Categories')
BEGIN
    CREATE TABLE tbl_Categories (
        CategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Categories PRIMARY KEY,
        CategoryName NVARCHAR(500) NOT NULL,
        CategoryShortDesc NVARCHAR(1000) NULL,
        CategoryDesc NVARCHAR(MAX) NULL,
        Image VARCHAR(50) NULL,
        ParentId INT NULL,
        SortOrder INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Products')
BEGIN
    CREATE TABLE tbl_Products (
        ProductId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Products PRIMARY KEY,
        ProductCode VARCHAR(20) NULL,
        ProductName NVARCHAR(500) NOT NULL,
        ProductNameEn NVARCHAR(500) NULL,
        ProductShortName NVARCHAR(100) NULL,
        UnitName NVARCHAR(100) NULL,
        UnitNameEn NVARCHAR(100) NULL,
        ProductSlogan NVARCHAR(400) NULL,
        ProductShortDesc NVARCHAR(2000) NULL,
        ProductLargeImage VARCHAR(500) NULL,
        ProductSmallImage VARCHAR(500) NULL,
        ProductIconClass VARCHAR(50) NULL,
        ProductDocument VARCHAR(200) NULL,
        ProductTags NVARCHAR(500) NULL,
        ProductDeployment NVARCHAR(4000) NULL,
        CategoryId INT NULL,
        Price DECIMAL(18,2) NULL,
        Quantity INT NULL,
        SortOrder INT NULL,
        Rewrite VARCHAR(200) NULL,
        TitleBrowser NVARCHAR(500) NULL,
        MetaDescription NVARCHAR(2000) NULL,
        MetaKeyword NVARCHAR(2000) NULL,
        Others NVARCHAR(4000) NULL,
        ProductCreatedDate DATETIME NULL CONSTRAINT DF_tbl_Products_ProductCreatedDate DEFAULT GETDATE(),
        CONSTRAINT FK_tbl_Products_tbl_Categories FOREIGN KEY (CategoryId) REFERENCES tbl_Categories(CategoryId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ServiceTypes')
BEGIN
    CREATE TABLE tbl_ServiceTypes (
        ServiceTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ServiceTypes PRIMARY KEY,
        ServiceTypeName NVARCHAR(200) NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Services')
BEGIN
    CREATE TABLE tbl_Services (
        ServiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Services PRIMARY KEY,
        ServiceName NVARCHAR(2000) NOT NULL,
        ServiceNameEn NVARCHAR(500) NULL,
        UnitName NVARCHAR(100) NULL,
        UnitNameEn NVARCHAR(100) NULL,
        Price DECIMAL(18,2) NULL,
        ServiceTypeId INT NULL,
        ServiceRegion INT NULL,
        ServiceImageIcon VARCHAR(50) NULL,
        FTPAccounts INT NULL,
        MySQL INT NULL,
        HasChild BIT NOT NULL CONSTRAINT DF_tbl_Services_HasChild DEFAULT 0,
        Rewrite VARCHAR(300) NULL,
        TitleBrowser NVARCHAR(500) NULL,
        MetaDescription NVARCHAR(2000) NULL,
        MetaKeyword NVARCHAR(2000) NULL,
        Others NVARCHAR(4000) NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_Services_Status DEFAULT 0,
        DateCreated DATETIME NULL CONSTRAINT DF_tbl_Services_DateCreated DEFAULT GETDATE(),
        DateModified DATETIME NULL,
        CONSTRAINT FK_tbl_Services_tbl_ServiceTypes FOREIGN KEY (ServiceTypeId) REFERENCES tbl_ServiceTypes(ServiceTypeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_FileStorage')
BEGIN
    CREATE TABLE tbl_FileStorage (
        FileId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_FileStorage PRIMARY KEY,
        TenantCode VARCHAR(100) NOT NULL,
        ObjectType VARCHAR(50) NOT NULL,
        FileType VARCHAR(100) NOT NULL,
        FileName NVARCHAR(300) NOT NULL,
        FilePath NVARCHAR(1000) NOT NULL,
        StorageKey VARCHAR(1000) NOT NULL,
        ContentType VARCHAR(255) NOT NULL,
        FileSize BIGINT NOT NULL,
        Sha256 CHAR(64) NOT NULL,
        UploadedByEmployeeId INT NULL,
        UploadedDate DATETIME NOT NULL CONSTRAINT DF_tbl_FileStorage_UploadedDate DEFAULT GETDATE()
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.3. NGHIỆP VỤ BÁN HÀNG CŨ (QUOTATION, ORDERS, INVOICES, PAYMENTS, DELIVERY)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Quotation')
BEGIN
    CREATE TABLE tbl_Quotation (
        QuotationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Quotation PRIMARY KEY,
        QuotationNo VARCHAR(50) NOT NULL,
        QuotationDate DATETIME NOT NULL,
        CustomerId INT NOT NULL,
        EmployeeId INT NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        QuatationStatus VARCHAR(30) NOT NULL CONSTRAINT DF_tbl_Quotation_Status DEFAULT 'Draft',
        CONSTRAINT FK_tbl_Quotation_tbl_Customers FOREIGN KEY (CustomerId) REFERENCES tbl_Customers(CustomerId),
        CONSTRAINT FK_tbl_Quotation_tbl_Employee FOREIGN KEY (EmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_QuotationDetail')
BEGIN
    CREATE TABLE tbl_QuotationDetail (
        QuotationDetailId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_QuotationDetail PRIMARY KEY,
        QuotationId INT NOT NULL,
        ProductId INT NULL,
        ServiceId INT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_tbl_QuotationDetail_tbl_Quotation FOREIGN KEY (QuotationId) REFERENCES tbl_Quotation(QuotationId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Orders')
BEGIN
    CREATE TABLE tbl_Orders (
        OrderId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Orders PRIMARY KEY,
        CustomerId INT NOT NULL,
        EmployeeId INT NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        OrderStatus TINYINT NOT NULL CONSTRAINT DF_tbl_Orders_OrderStatus DEFAULT 0,
        OrderComment NVARCHAR(4000) NULL,
        NoteFromAdmin NVARCHAR(1000) NULL,
        DateCreated DATETIME NOT NULL CONSTRAINT DF_tbl_Orders_DateCreated DEFAULT GETDATE(),
        DateExpired DATETIME NULL,
        UpdatedDate DATETIME NULL,
        CONSTRAINT FK_tbl_Orders_tbl_Customers FOREIGN KEY (CustomerId) REFERENCES tbl_Customers(CustomerId),
        CONSTRAINT FK_tbl_Orders_tbl_Employee FOREIGN KEY (EmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_OrderDetails')
BEGIN
    CREATE TABLE tbl_OrderDetails (
        OrderDetailsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_OrderDetails PRIMARY KEY,
        OrderId INT NOT NULL,
        ProductId INT NULL,
        ServiceId INT NULL,
        Quantity INT NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        NameDetails NVARCHAR(2000) NULL,
        StartDate DATETIME NULL,
        DateExpired DATETIME NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_OrderDetails_Status DEFAULT 0,
        DateCreated DATETIME NOT NULL CONSTRAINT DF_tbl_OrderDetails_DateCreated DEFAULT GETDATE(),
        CONSTRAINT FK_tbl_OrderDetails_tbl_Orders FOREIGN KEY (OrderId) REFERENCES tbl_Orders(OrderId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_DeliveryOrder')
BEGIN
    CREATE TABLE tbl_DeliveryOrder (
        DeliveryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_DeliveryOrder PRIMARY KEY,
        DeliveryNo VARCHAR(50) NOT NULL,
        OrderId INT NOT NULL,
        DeliveryDate DATETIME NOT NULL,
        ReceiverName NVARCHAR(200) NOT NULL,
        ReceiverPhone VARCHAR(20) NOT NULL,
        DeliveryAddress NVARCHAR(1000) NOT NULL,
        DeliveryStatus VARCHAR(30) NOT NULL CONSTRAINT DF_tbl_DeliveryOrder_Status DEFAULT 'Pending',
        CONSTRAINT FK_tbl_DeliveryOrder_tbl_Orders FOREIGN KEY (OrderId) REFERENCES tbl_Orders(OrderId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_DeliveryDetail')
BEGIN
    CREATE TABLE tbl_DeliveryDetail (
        DeliveryDetailId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_DeliveryDetail PRIMARY KEY,
        DeliveryId INT NOT NULL,
        OrderDetailsId INT NOT NULL,
        DeliveredQuantity INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        CONSTRAINT FK_tbl_DeliveryDetail_tbl_DeliveryOrder FOREIGN KEY (DeliveryId) REFERENCES tbl_DeliveryOrder(DeliveryId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Invoice')
BEGIN
    CREATE TABLE tbl_Invoice (
        InvoiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Invoice PRIMARY KEY,
        InvoiceNo VARCHAR(50) NOT NULL,
        OrderId INT NOT NULL,
        InvoiceDate DATETIME NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        InvoiceStatus VARCHAR(30) NOT NULL CONSTRAINT DF_tbl_Invoice_Status DEFAULT 'Unpaid',
        CONSTRAINT FK_tbl_Invoice_tbl_Orders FOREIGN KEY (OrderId) REFERENCES tbl_Orders(OrderId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Payment')
BEGIN
    CREATE TABLE tbl_Payment (
        PaymentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Payment PRIMARY KEY,
        InvoiceId INT NOT NULL,
        PaymentDate DATETIME NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentMethod VARCHAR(50) NOT NULL,
        ReferenceNo VARCHAR(100) NULL,
        CONSTRAINT FK_tbl_Payment_tbl_Invoice FOREIGN KEY (InvoiceId) REFERENCES tbl_Invoice(InvoiceId)
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.4. ĐỘNG CƠ MẪU HỢP ĐỒNG (CONTRACT TEMPLATE ENGINE - 100% EF CORE CONFIGURATION)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplate')
BEGIN
    CREATE TABLE tbl_ContractTemplate (
        TemplateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplate PRIMARY KEY,
        TemplateCode VARCHAR(50) NOT NULL,
        TemplateName NVARCHAR(500) NOT NULL,
        TemplateNameEn NVARCHAR(500) NULL,
        Description NVARCHAR(2000) NULL,
        DocumentType INT NOT NULL,
        LanguageMode TINYINT NOT NULL DEFAULT 1,
        IsActive BIT NOT NULL CONSTRAINT DF_tbl_ContractTemplate_IsActive DEFAULT 1,
        CurrentPublishedVersionId INT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTemplate_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplate_TemplateCode CHECK (LEN(LTRIM(RTRIM([TemplateCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplate_DocumentType CHECK ([DocumentType] IN (1, 2, 3, 4, 5, 6, 7, 8, 99)),
        CONSTRAINT CK_tbl_ContractTemplate_LanguageMode CHECK ([LanguageMode] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractTemplate_CurrentPublishedVersionId CHECK ([CurrentPublishedVersionId] IS NULL OR [CurrentPublishedVersionId] > 0),
        CONSTRAINT UX_tbl_ContractTemplate_TemplateCode UNIQUE (TemplateCode)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplate_DocumentType_IsActive ON tbl_ContractTemplate(DocumentType, IsActive);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplate_CurrentPublishedVersionId ON tbl_ContractTemplate(CurrentPublishedVersionId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateVersion')
BEGIN
    CREATE TABLE tbl_ContractTemplateVersion (
        TemplateVersionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateVersion PRIMARY KEY,
        TemplateId INT NOT NULL,
        VersionNo INT NOT NULL,
        ContractDirection INT NOT NULL DEFAULT 1,
        Status TINYINT NOT NULL DEFAULT 0,
        ValidationStatus TINYINT NOT NULL DEFAULT 0,
        ValidationErrors NVARCHAR(MAX) NULL,
        DocumentFileId INT NULL,
        DocumentHash VARCHAR(64) NULL,
        PlaceholderBindingHash VARCHAR(64) NULL,
        PreviewFileId INT NULL,
        PreviewSourceHash CHAR(64) NULL,
        PreviewedByEmployeeId INT NULL,
        PreviewedAt DATETIME2 NULL,
        ValidatedByEmployeeId INT NULL,
        ValidatedDate DATETIME2 NULL,
        PublishedByEmployeeId INT NULL,
        PublishedDate DATETIME2 NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTemplateVersion_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateVersion_VersionNo CHECK ([VersionNo] > 0),
        CONSTRAINT CK_tbl_ContractTemplateVersion_Status CHECK ([Status] IN (0, 1, 2)),
        CONSTRAINT CK_tbl_ContractTemplateVersion_ValidationStatus CHECK ([ValidationStatus] IN (0, 1, 2)),
        CONSTRAINT CK_tbl_ContractTemplateVersion_ContractDirection CHECK ([ContractDirection] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractTemplateVersion_Document CHECK (([DocumentFileId] IS NULL AND [DocumentHash] IS NULL) OR ([DocumentFileId] > 0 AND [DocumentHash] IS NOT NULL AND LEN([DocumentHash]) = 64)),
        CONSTRAINT UX_tbl_ContractTemplateVersion_Template_Version UNIQUE (TemplateId, VersionNo),
        CONSTRAINT FK_tbl_ContractTemplateVersion_Template FOREIGN KEY (TemplateId) REFERENCES tbl_ContractTemplate(TemplateId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplateVersion_Direction_Status ON tbl_ContractTemplateVersion(ContractDirection, Status);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateItemTableColumnLayout')
BEGIN
    CREATE TABLE tbl_ContractTemplateItemTableColumnLayout (
        ItemTableColumnLayoutId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateItemTableColumnLayout PRIMARY KEY,
        TemplateVersionId INT NOT NULL,
        ColumnKey VARCHAR(32) NOT NULL,
        DisplayOrder INT NOT NULL,
        WidthBps INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        CONSTRAINT CK_tbl_ContractTemplateItemTableColumnLayout_DisplayOrder CHECK ([DisplayOrder] BETWEEN 0 AND 7),
        CONSTRAINT CK_tbl_ContractTemplateItemTableColumnLayout_WidthBps CHECK ([WidthBps] BETWEEN 250 AND 10000),
        CONSTRAINT UX_tbl_ContractTemplateItemTableColumnLayout_Version_Key UNIQUE (TemplateVersionId, ColumnKey),
        CONSTRAINT UX_tbl_ContractTemplateItemTableColumnLayout_Version_Order UNIQUE (TemplateVersionId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractTemplateItemTableColumnLayout_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateField')
BEGIN
    CREATE TABLE tbl_ContractTemplateField (
        TemplateFieldId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateField PRIMARY KEY,
        TemplateVersionId INT NOT NULL,
        PlaceholderKey VARCHAR(100) NOT NULL,
        SourceFieldKey VARCHAR(200) NOT NULL,
        FieldLabel NVARCHAR(300) NOT NULL,
        DataSource VARCHAR(500) NOT NULL,
        DefaultValue NVARCHAR(2000) NULL,
        FormatString VARCHAR(100) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_tbl_ContractTemplateField_IsRequired DEFAULT 0,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractTemplateField_DisplayOrder DEFAULT 0,
        DefinitionRowVersion VARBINARY(8) NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTemplateField_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateField_TemplateVersionId CHECK ([TemplateVersionId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateField_PlaceholderKey CHECK (LEN(LTRIM(RTRIM([PlaceholderKey]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateField_DataSource CHECK (LEN(LTRIM(RTRIM([DataSource]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateField_DisplayOrder CHECK ([DisplayOrder] >= 0),
        CONSTRAINT UX_tbl_ContractTemplateField_Version_Placeholder UNIQUE (TemplateVersionId, PlaceholderKey),
        CONSTRAINT FK_tbl_ContractTemplateField_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplateField_Version_DisplayOrder ON tbl_ContractTemplateField(TemplateVersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateTerm')
BEGIN
    CREATE TABLE tbl_ContractTemplateTerm (
        TemplateTermId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateTerm PRIMARY KEY,
        TemplateVersionId INT NOT NULL,
        TermCode VARCHAR(100) NOT NULL,
        TermTitle NVARCHAR(500) NOT NULL,
        TermTitleEn NVARCHAR(500) NULL,
        TermContent NVARCHAR(MAX) NULL,
        TermContentEn NVARCHAR(MAX) NULL,
        IsNegotiable BIT NOT NULL CONSTRAINT DF_tbl_ContractTemplateTerm_IsNegotiable DEFAULT 0,
        TermKind TINYINT NOT NULL CONSTRAINT DF_tbl_ContractTemplateTerm_TermKind DEFAULT 0,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractTemplateTerm_DisplayOrder DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTemplateTerm_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateTerm_TemplateVersionId CHECK ([TemplateVersionId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateTerm_TermCode CHECK (LEN(LTRIM(RTRIM([TermCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateTerm_DisplayOrder CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_tbl_ContractTemplateTerm_TermKind CHECK ([TermKind] IN (0, 1)),
        CONSTRAINT CK_TblContractTemplateTerm_BilingualContentMode CHECK ([BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)),
        CONSTRAINT UX_tbl_ContractTemplateTerm_Version_TermCode UNIQUE (TemplateVersionId, TermCode),
        CONSTRAINT FK_tbl_ContractTemplateTerm_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplateTerm_Version_DisplayOrder ON tbl_ContractTemplateTerm(TemplateVersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplatePaymentMilestone')
BEGIN
    CREATE TABLE tbl_ContractTemplatePaymentMilestone (
        TemplatePaymentMilestoneId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplatePaymentMilestone PRIMARY KEY,
        TemplateTermId INT NOT NULL,
        TemplateVersionId INT NOT NULL,
        MilestoneCode VARCHAR(100) NOT NULL,
        TitleVi NVARCHAR(500) NOT NULL,
        TitleEn NVARCHAR(500) NULL,
        PaymentPercent DECIMAL(9,4) NOT NULL,
        DueOffsetDays INT NOT NULL DEFAULT 0,
        DueAnchor TINYINT NOT NULL DEFAULT 1,
        DayCountMode TINYINT NOT NULL DEFAULT 1,
        ConditionVi NVARCHAR(2000) NULL,
        ConditionEn NVARCHAR(2000) NULL,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplatePaymentMilestone_Percent CHECK ([PaymentPercent] > 0 AND [PaymentPercent] <= 100),
        CONSTRAINT CK_tbl_ContractTemplatePaymentMilestone_DueOffset CHECK ([DueOffsetDays] >= 0),
        CONSTRAINT CK_tbl_ContractTemplatePaymentMilestone_DueAnchor CHECK ([DueAnchor] IN (1, 2, 3, 4, 5)),
        CONSTRAINT CK_tbl_ContractTemplatePaymentMilestone_DayCount CHECK ([DayCountMode] IN (1, 2)),
        CONSTRAINT UX_tbl_ContractTemplatePaymentMilestone_Version_Code UNIQUE (TemplateVersionId, MilestoneCode),
        CONSTRAINT UX_tbl_ContractTemplatePaymentMilestone_Term_Order UNIQUE (TemplateTermId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractTemplatePaymentMilestone_Term FOREIGN KEY (TemplateTermId) REFERENCES tbl_ContractTemplateTerm(TemplateTermId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractTemplatePaymentMilestone_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateLegalBasis')
BEGIN
    CREATE TABLE tbl_ContractTemplateLegalBasis (
        TemplateLegalBasisId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateLegalBasis PRIMARY KEY,
        TemplateVersionId INT NOT NULL,
        BasisCode VARCHAR(100) NOT NULL,
        ContentVi NVARCHAR(MAX) NOT NULL,
        ContentEn NVARCHAR(MAX) NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractTemplateLegalBasis_DisplayOrder DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTemplateLegalBasis_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateLegalBasis_TemplateVersionId CHECK ([TemplateVersionId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateLegalBasis_BasisCode CHECK (LEN(LTRIM(RTRIM([BasisCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateLegalBasis_ContentVi CHECK (LEN(LTRIM(RTRIM([ContentVi]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateLegalBasis_DisplayOrder CHECK ([DisplayOrder] >= 0),
        CONSTRAINT UX_tbl_ContractTemplateLegalBasis_Version_BasisCode UNIQUE (TemplateVersionId, BasisCode),
        CONSTRAINT FK_tbl_ContractTemplateLegalBasis_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplateLegalBasis_Version_DisplayOrder ON tbl_ContractTemplateLegalBasis(TemplateVersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateAppendix')
BEGIN
    CREATE TABLE tbl_ContractTemplateAppendix (
        TemplateAppendixId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateAppendix PRIMARY KEY,
        TemplateVersionId INT NOT NULL,
        AppendixCode VARCHAR(50) NOT NULL,
        AppendixName NVARCHAR(500) NOT NULL,
        AppendixNameEn NVARCHAR(500) NULL,
        AppendixDescription NVARCHAR(2000) NULL,
        IsRequired BIT NOT NULL DEFAULT 0,
        IsSelectedByDefault BIT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedEmployeeId INT NOT NULL,
        UpdatedEmployeeId INT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateAppendix_Code CHECK (LEN(LTRIM(RTRIM([AppendixCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateAppendix_Name CHECK (LEN(LTRIM(RTRIM([AppendixName]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateAppendix_Order CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_tbl_ContractTemplateAppendix_RequiredDefault CHECK ([IsRequired] = 0 OR [IsSelectedByDefault] = 1),
        CONSTRAINT UX_tbl_ContractTemplateAppendix_Version_Code UNIQUE (TemplateVersionId, AppendixCode),
        CONSTRAINT UX_tbl_ContractTemplateAppendix_Version_Order UNIQUE (TemplateVersionId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractTemplateAppendix_Version FOREIGN KEY (TemplateVersionId) REFERENCES tbl_ContractTemplateVersion(TemplateVersionId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateAppendixTerm')
BEGIN
    CREATE TABLE tbl_ContractTemplateAppendixTerm (
        TemplateAppendixTermId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateAppendixTerm PRIMARY KEY,
        TemplateAppendixId INT NOT NULL,
        TermCode VARCHAR(100) NOT NULL,
        TermTitle NVARCHAR(1000) NOT NULL,
        TermTitleEn NVARCHAR(1000) NULL,
        TermContent NVARCHAR(MAX) NULL,
        TermContentEn NVARCHAR(MAX) NULL,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedEmployeeId INT NOT NULL,
        UpdatedEmployeeId INT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateAppendixTerm_Code CHECK (LEN(LTRIM(RTRIM([TermCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateAppendixTerm_Title CHECK (LEN(LTRIM(RTRIM([TermTitle]))) > 0),
        CONSTRAINT CK_tbl_ContractTemplateAppendixTerm_Order CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_TblContractTemplateAppendixTerm_BilingualContentMode CHECK ([BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)),
        CONSTRAINT UX_tbl_ContractTemplateAppendixTerm_Appendix_Code UNIQUE (TemplateAppendixId, TermCode),
        CONSTRAINT UX_tbl_ContractTemplateAppendixTerm_Appendix_Order UNIQUE (TemplateAppendixId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractTemplateAppendixTerm_Appendix FOREIGN KEY (TemplateAppendixId) REFERENCES tbl_ContractTemplateAppendix(TemplateAppendixId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateAudit')
BEGIN
    CREATE TABLE tbl_ContractTemplateAudit (
        ContractTemplateAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateAudit PRIMARY KEY,
        TenantId INT NOT NULL,
        TemplateId INT NOT NULL,
        TemplateVersionId INT NOT NULL,
        ActorEmployeeId INT NOT NULL,
        ActionType VARCHAR(64) NOT NULL,
        Result VARCHAR(32) NOT NULL,
        FailureCode VARCHAR(64) NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IpAddress VARCHAR(45) NULL,
        UserAgent NVARCHAR(1024) NULL,
        CorrelationId VARCHAR(100) NOT NULL,
        CONSTRAINT CK_tbl_ContractTemplateAudit_TenantId CHECK ([TenantId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateAudit_TemplateId CHECK ([TemplateId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateAudit_TemplateVersionId CHECK ([TemplateVersionId] > 0),
        CONSTRAINT CK_tbl_ContractTemplateAudit_ActorEmployeeId CHECK ([ActorEmployeeId] > 0)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTemplateAudit_Tenant_Version_Occurred ON tbl_ContractTemplateAudit(TenantId, TemplateVersionId, OccurredAt);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTemplateAuditValue')
BEGIN
    CREATE TABLE tbl_ContractTemplateAuditValue (
        ContractTemplateAuditValueId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTemplateAuditValue PRIMARY KEY,
        ContractTemplateAuditId BIGINT NOT NULL,
        ValueSide TINYINT NOT NULL,
        FieldCode INT NOT NULL,
        IsNull BIT NOT NULL,
        IntegerValue INT NULL,
        LongValue BIGINT NULL,
        StringValue VARCHAR(32) NULL,
        CONSTRAINT UX_tbl_ContractTemplateAuditValue_Audit_Side_Field UNIQUE (ContractTemplateAuditId, ValueSide, FieldCode),
        CONSTRAINT FK_tbl_ContractTemplateAuditValue_Audit FOREIGN KEY (ContractTemplateAuditId) REFERENCES tbl_ContractTemplateAudit(ContractTemplateAuditId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractPlaceholderDefinition')
BEGIN
    CREATE TABLE tbl_ContractPlaceholderDefinition (
        PlaceholderDefinitionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractPlaceholderDefinition PRIMARY KEY,
        PlaceholderKey VARCHAR(100) NOT NULL,
        FieldLabel NVARCHAR(300) NOT NULL,
        SourceFieldKey VARCHAR(200) NOT NULL,
        DefaultValue NVARCHAR(2000) NULL,
        FormatString VARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL DEFAULT 0,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UX_tbl_ContractPlaceholderDefinition_Key UNIQUE (PlaceholderKey)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractPlaceholderAudit')
BEGIN
    CREATE TABLE tbl_ContractPlaceholderAudit (
        PlaceholderAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractPlaceholderAudit PRIMARY KEY,
        PlaceholderKey VARCHAR(100) NOT NULL,
        ActionType VARCHAR(64) NOT NULL,
        ActorEmployeeId INT NOT NULL,
        PreviousSourceFieldKey VARCHAR(200) NULL,
        PreviousFormatString VARCHAR(100) NULL,
        PreviousIsActive BIT NULL,
        NewSourceFieldKey VARCHAR(200) NOT NULL,
        NewFormatString VARCHAR(100) NULL,
        NewIsActive BIT NOT NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractPlaceholderAudit_Key_Occurred ON tbl_ContractPlaceholderAudit(PlaceholderKey, OccurredAt);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.5. HỢP ĐỒNG ĐIỆN TỬ ONLINE [HƯỚNG 1] (LIFECYCLE, VERSIONING, PHÊ DUYỆT, OTP & KÝ SỐ)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Contract')
BEGIN
    CREATE TABLE tbl_Contract (
        ContractId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Contract PRIMARY KEY,
        ContractCode VARCHAR(50) NULL,
        ContractName NVARCHAR(1000) NOT NULL,
        ContractNameEn NVARCHAR(1000) NULL,
        CustomerId INT NOT NULL,
        EmployeeId INT NOT NULL,
        TemplateVersionId INT NOT NULL,
        CurrentVersionId INT NULL,
        ParentContractId INT NULL,
        ContractType INT NOT NULL DEFAULT 1,
        ContractDirection INT NOT NULL DEFAULT 1,
        LanguageMode TINYINT NOT NULL CONSTRAINT DF_tbl_Contract_LanguageMode DEFAULT 1,
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_Contract_Status DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_Contract_TotalAmount DEFAULT 0,
        Subtotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_Contract_Subtotal DEFAULT 0,
        TotalDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_Contract_TotalDiscount DEFAULT 0,
        TotalVat DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_Contract_TotalVat DEFAULT 0,
        CurrencyCode CHAR(3) NOT NULL CONSTRAINT DF_tbl_Contract_CurrencyCode DEFAULT 'VND',
        SignDate DATETIME2 NULL,
        EffectiveDate DATETIME2 NULL,
        ExpireDate DATETIME2 NULL,
        CurrentVerificationPhoneId INT NULL,
        CurrentCustomerAccessLinkId INT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_Contract_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdateDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_Contract_Status CHECK ([Status] IN (0, 1, 2, 3, 4, 5, 6, 7)),
        CONSTRAINT CK_tbl_Contract_ContractType CHECK ([ContractType] IN (1, 2, 3)),
        CONSTRAINT CK_tbl_Contract_ContractDirection CHECK ([ContractDirection] IN (1, 2)),
        CONSTRAINT CK_tbl_Contract_LanguageMode CHECK ([LanguageMode] IN (1, 2)),
        CONSTRAINT CK_tbl_Contract_TemplateVersionId CHECK ([TemplateVersionId] > 0),
        CONSTRAINT CK_tbl_Contract_FinancialTotals CHECK ([Subtotal] >= 0 AND [TotalDiscount] >= 0 AND [TotalVat] >= 0 AND [TotalAmount] >= 0)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_Contract_ContractCode ON tbl_Contract(ContractCode) WHERE [ContractCode] IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_tbl_Contract_CustomerId ON tbl_Contract(CustomerId);
    CREATE NONCLUSTERED INDEX IX_tbl_Contract_EmployeeId_Status ON tbl_Contract(EmployeeId, Status);
    CREATE NONCLUSTERED INDEX IX_tbl_Contract_TemplateVersionId ON tbl_Contract(TemplateVersionId);
    CREATE NONCLUSTERED INDEX IX_tbl_Contract_CurrentVersionId ON tbl_Contract(CurrentVersionId);
    CREATE NONCLUSTERED INDEX IX_tbl_Contract_Direction_Status ON tbl_Contract(ContractDirection, Status);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractVersion')
BEGIN
    CREATE TABLE tbl_ContractVersion (
        VersionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractVersion PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionNo INT NOT NULL,
        SourceVersionId INT NULL,
        TemplateVersionId INT NOT NULL,
        ChangeNote NVARCHAR(2000) NULL,
        Subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalVat DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CurrencyCode CHAR(3) NOT NULL CONSTRAINT DF_tbl_ContractVersion_CurrencyCode DEFAULT 'VND',
        IsLocked BIT NOT NULL CONSTRAINT DF_tbl_ContractVersion_IsLocked DEFAULT 0,
        LockedDate DATETIME2 NULL,
        LockedByEmployeeId INT NULL,
        SnapshotHash CHAR(64) NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractVersion_CreatedDate DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractVersion_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractVersion_VersionNo CHECK ([VersionNo] > 0),
        CONSTRAINT CK_tbl_ContractVersion_CurrencyCode CHECK ([CurrencyCode] IN ('VND', 'USD')),
        CONSTRAINT CK_tbl_ContractVersion_FinancialTotals CHECK ([Subtotal] >= 0 AND [TotalDiscount] >= 0 AND [TotalVat] >= 0 AND [TotalAmount] >= 0),
        CONSTRAINT CK_tbl_ContractVersion_LockState CHECK (([IsLocked] = 0 AND [LockedDate] IS NULL AND [LockedByEmployeeId] IS NULL) OR ([IsLocked] = 1 AND [LockedDate] IS NOT NULL AND [LockedByEmployeeId] IS NOT NULL AND [SnapshotHash] IS NOT NULL)),
        CONSTRAINT UX_tbl_ContractVersion_ContractId_VersionNo UNIQUE (ContractId, VersionNo),
        CONSTRAINT FK_tbl_ContractVersion_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractVersion_TemplateVersionId ON tbl_ContractVersion(TemplateVersionId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractItem')
BEGIN
    CREATE TABLE tbl_ContractItem (
        ContractItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractItem PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        SourceProductId INT NULL,
        SourceServiceId INT NULL,
        ItemCode VARCHAR(100) NULL,
        ItemName NVARCHAR(500) NOT NULL,
        ItemNameEn NVARCHAR(500) NULL,
        ItemDescription NVARCHAR(MAX) NULL,
        ItemDescriptionEn NVARCHAR(MAX) NULL,
        UnitName NVARCHAR(100) NULL,
        UnitNameEn NVARCHAR(100) NULL,
        Quantity DECIMAL(18,4) NOT NULL CONSTRAINT DF_tbl_ContractItem_Quantity DEFAULT 1,
        UnitPrice DECIMAL(18,2) NOT NULL,
        LineSubtotal DECIMAL(18,2) NOT NULL,
        DiscountPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_tbl_ContractItem_DiscountPercent DEFAULT 0,
        DiscountMode TINYINT NOT NULL CONSTRAINT DF_tbl_ContractItem_DiscountMode DEFAULT 0,
        FixedDiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_ContractItem_FixedDiscountAmount DEFAULT 0,
        DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_ContractItem_DiscountAmount DEFAULT 0,
        VatPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_tbl_ContractItem_VatPercent DEFAULT 0,
        IsTaxable BIT NOT NULL CONSTRAINT DF_tbl_ContractItem_IsTaxable DEFAULT 1,
        VatAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tbl_ContractItem_VatAmount DEFAULT 0,
        LineTotal DECIMAL(18,2) NOT NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractItem_DisplayOrder DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractItem_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractItem_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractItem_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT CK_tbl_ContractItem_Quantity CHECK ([Quantity] > 0),
        CONSTRAINT CK_tbl_ContractItem_UnitPrice CHECK ([UnitPrice] >= 0),
        CONSTRAINT CK_tbl_ContractItem_LineSubtotal CHECK ([LineSubtotal] >= 0),
        CONSTRAINT CK_tbl_ContractItem_LineTotal CHECK ([LineTotal] >= 0),
        CONSTRAINT FK_tbl_ContractItem_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractItem_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractItem_Version_DisplayOrder ON tbl_ContractItem(VersionId, DisplayOrder);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractItem_Contract_Version ON tbl_ContractItem(ContractId, VersionId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractTerm')
BEGIN
    CREATE TABLE tbl_ContractTerm (
        TermId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractTerm PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        SourceTemplateTermId INT NULL,
        TermCode VARCHAR(100) NOT NULL,
        TermTitle NVARCHAR(500) NOT NULL,
        TermTitleEn NVARCHAR(500) NULL,
        TermContent NVARCHAR(MAX) NULL,
        TermContentEn NVARCHAR(MAX) NULL,
        IsNegotiable BIT NOT NULL CONSTRAINT DF_tbl_ContractTerm_IsNegotiable DEFAULT 0,
        TermKind TINYINT NOT NULL CONSTRAINT DF_tbl_ContractTerm_TermKind DEFAULT 0,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractTerm_DisplayOrder DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractTerm_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractTerm_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractTerm_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT CK_tbl_ContractTerm_TermCode CHECK (LEN(LTRIM(RTRIM([TermCode]))) > 0),
        CONSTRAINT CK_tbl_ContractTerm_DisplayOrder CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_TblContractTerm_BilingualContentMode CHECK ([BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)),
        CONSTRAINT UX_tbl_ContractTerm_Version_TermCode UNIQUE (VersionId, TermCode),
        CONSTRAINT FK_tbl_ContractTerm_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractTerm_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractTerm_Version_DisplayOrder ON tbl_ContractTerm(VersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractPaymentMilestone')
BEGIN
    CREATE TABLE tbl_ContractPaymentMilestone (
        PaymentMilestoneId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractPaymentMilestone PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        TermId INT NOT NULL,
        SourceTemplatePaymentMilestoneId INT NULL,
        MilestoneCode VARCHAR(100) NOT NULL,
        TitleVi NVARCHAR(500) NOT NULL,
        TitleEn NVARCHAR(500) NULL,
        PaymentPercent DECIMAL(9,4) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        DueOffsetDays INT NOT NULL DEFAULT 0,
        DueAnchor TINYINT NOT NULL DEFAULT 1,
        DayCountMode TINYINT NOT NULL DEFAULT 1,
        AnchorDate DATE NULL,
        DueDate DATE NULL,
        PaymentStatus TINYINT NOT NULL CONSTRAINT DF_tbl_ContractPaymentMilestone_Status DEFAULT 0,
        PaidAt DATETIME2 NULL,
        PaidByEmployeeId INT NULL,
        ConditionVi NVARCHAR(2000) NULL,
        ConditionEn NVARCHAR(2000) NULL,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractPaymentMilestone_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractPaymentMilestone_Percent CHECK ([PaymentPercent] > 0 AND [PaymentPercent] <= 100),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_Amount CHECK ([Amount] >= 0),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_DueOffset CHECK ([DueOffsetDays] >= 0),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_DueAnchor CHECK ([DueAnchor] IN (1, 2, 3, 4, 5)),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_DayCount CHECK ([DayCountMode] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_PaymentStatus CHECK ([PaymentStatus] IN (0, 1)),
        CONSTRAINT CK_tbl_ContractPaymentMilestone_PaidMetadata CHECK (([PaymentStatus] = 0 AND [PaidAt] IS NULL AND [PaidByEmployeeId] IS NULL) OR ([PaymentStatus] = 1 AND [PaidAt] IS NOT NULL AND [PaidByEmployeeId] IS NOT NULL)),
        CONSTRAINT UX_tbl_ContractPaymentMilestone_Version_Code UNIQUE (VersionId, MilestoneCode),
        CONSTRAINT FK_tbl_ContractPaymentMilestone_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractPaymentMilestone_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId),
        CONSTRAINT FK_tbl_ContractPaymentMilestone_Term FOREIGN KEY (TermId) REFERENCES tbl_ContractTerm(TermId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractPaymentMilestone_Contract_Version_Order ON tbl_ContractPaymentMilestone(ContractId, VersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractPaymentLedger')
BEGIN
    CREATE TABLE tbl_ContractPaymentLedger (
        ContractPaymentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractPaymentLedger PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        PaymentMilestoneId INT NULL,
        ReferenceCode NVARCHAR(100) NOT NULL,
        PaymentDate DATE NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CurrencyCode VARCHAR(3) NOT NULL,
        PaymentMethod NVARCHAR(100) NOT NULL,
        EvidenceFileId INT NULL,
        Status TINYINT NOT NULL DEFAULT 1,
        VoidReason NVARCHAR(1000) NULL,
        CreatedByEmployeeId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        VoidedByEmployeeId INT NULL,
        VoidedAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractPaymentLedger_Amount CHECK ([Amount] > 0),
        CONSTRAINT CK_tbl_ContractPaymentLedger_Status CHECK ([Status] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractPaymentLedger_VoidMetadata CHECK (([Status] = 1 AND [VoidReason] IS NULL AND [VoidedByEmployeeId] IS NULL AND [VoidedAt] IS NULL) OR ([Status] = 2 AND [VoidReason] IS NOT NULL AND [VoidedByEmployeeId] IS NOT NULL AND [VoidedAt] IS NOT NULL)),
        CONSTRAINT UX_tbl_ContractPaymentLedger_Version_Ref UNIQUE (VersionId, ReferenceCode),
        CONSTRAINT FK_tbl_ContractPaymentLedger_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractPaymentLedger_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId),
        CONSTRAINT FK_tbl_ContractPaymentLedger_Milestone FOREIGN KEY (PaymentMilestoneId) REFERENCES tbl_ContractPaymentMilestone(PaymentMilestoneId),
        CONSTRAINT FK_tbl_ContractPaymentLedger_Evidence FOREIGN KEY (EvidenceFileId) REFERENCES tbl_FileStorage(FileId)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_ContractPaymentLedger_ActiveMilestone ON tbl_ContractPaymentLedger(PaymentMilestoneId)
        WHERE [PaymentMilestoneId] IS NOT NULL AND [Status] = 1;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractLegalBasis')
BEGIN
    CREATE TABLE tbl_ContractLegalBasis (
        LegalBasisId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractLegalBasis PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        SourceTemplateLegalBasisId INT NULL,
        BasisCode VARCHAR(100) NOT NULL,
        ContentVi NVARCHAR(MAX) NOT NULL,
        ContentEn NVARCHAR(MAX) NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_tbl_ContractLegalBasis_DisplayOrder DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractLegalBasis_CreatedDate DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractLegalBasis_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractLegalBasis_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT CK_tbl_ContractLegalBasis_BasisCode CHECK (LEN(LTRIM(RTRIM([BasisCode]))) > 0),
        CONSTRAINT CK_tbl_ContractLegalBasis_ContentVi CHECK (LEN(LTRIM(RTRIM([ContentVi]))) > 0),
        CONSTRAINT CK_tbl_ContractLegalBasis_DisplayOrder CHECK ([DisplayOrder] >= 0),
        CONSTRAINT UX_tbl_ContractLegalBasis_Version_BasisCode UNIQUE (VersionId, BasisCode),
        CONSTRAINT FK_tbl_ContractLegalBasis_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractLegalBasis_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractLegalBasis_Contract_Version_DisplayOrder ON tbl_ContractLegalBasis(ContractId, VersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAppendix')
BEGIN
    CREATE TABLE tbl_ContractAppendix (
        AppendixId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAppendix PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        SourceTemplateAppendixId INT NULL,
        AppendixCode VARCHAR(50) NOT NULL,
        AppendixName NVARCHAR(500) NOT NULL,
        AppendixNameEn NVARCHAR(500) NULL,
        AppendixDescription NVARCHAR(2000) NULL,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedEmployeeId INT NOT NULL,
        UpdatedEmployeeId INT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractAppendix_Order CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_tbl_ContractAppendix_Code CHECK (LEN(LTRIM(RTRIM([AppendixCode]))) > 0),
        CONSTRAINT UX_tbl_ContractAppendix_Version_Code UNIQUE (VersionId, AppendixCode),
        CONSTRAINT UX_tbl_ContractAppendix_Version_Order UNIQUE (VersionId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractAppendix_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractAppendix_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAppendix_Contract_Version ON tbl_ContractAppendix(ContractId, VersionId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAppendixTerm')
BEGIN
    CREATE TABLE tbl_ContractAppendixTerm (
        AppendixTermId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAppendixTerm PRIMARY KEY,
        AppendixId INT NOT NULL,
        SourceTemplateAppendixTermId INT NULL,
        TermCode VARCHAR(100) NOT NULL,
        TermTitle NVARCHAR(1000) NOT NULL,
        TermTitleEn NVARCHAR(1000) NULL,
        TermContent NVARCHAR(MAX) NULL,
        TermContentEn NVARCHAR(MAX) NULL,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractAppendixTerm_Order CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_TblContractAppendixTerm_BilingualContentMode CHECK ([BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)),
        CONSTRAINT UX_tbl_ContractAppendixTerm_Appendix_Code UNIQUE (AppendixId, TermCode),
        CONSTRAINT UX_tbl_ContractAppendixTerm_Appendix_Order UNIQUE (AppendixId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractAppendixTerm_Appendix FOREIGN KEY (AppendixId) REFERENCES tbl_ContractAppendix(AppendixId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAttachment')
BEGIN
    CREATE TABLE tbl_ContractAttachment (
        AttachmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAttachment PRIMARY KEY,
        ContractId INT NOT NULL,
        ContractFileName NVARCHAR(300) NOT NULL,
        ContractFilePath VARCHAR(1000) NOT NULL,
        UploadDate DATETIME NOT NULL CONSTRAINT DF_tbl_ContractAttachment_UploadDate DEFAULT GETDATE(),
        DocumentType TINYINT NOT NULL CONSTRAINT DF_tbl_ContractAttachment_DocumentType DEFAULT 99,
        CONSTRAINT FK_tbl_ContractAttachment_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractSignedEvidence')
BEGIN
    CREATE TABLE tbl_ContractSignedEvidence (
        SignedEvidenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractSignedEvidence PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        FileId INT NOT NULL,
        Status TINYINT NOT NULL DEFAULT 1,
        SignDate DATE NOT NULL,
        UploadedByEmployeeId INT NOT NULL,
        UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        SupersededByEmployeeId INT NULL,
        SupersededAt DATETIME2 NULL,
        SupersedesEvidenceId INT NULL,
        SupersedeReason NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractSignedEvidence_Status CHECK ([Status] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractSignedEvidence_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractSignedEvidence_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT CK_tbl_ContractSignedEvidence_FileId CHECK ([FileId] > 0),
        CONSTRAINT UX_tbl_ContractSignedEvidence_FileId UNIQUE (FileId),
        CONSTRAINT FK_tbl_ContractSignedEvidence_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractSignedEvidence_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId),
        CONSTRAINT FK_tbl_ContractSignedEvidence_FileStorage FOREIGN KEY (FileId) REFERENCES tbl_FileStorage(FileId)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_ContractSignedEvidence_ActiveVersion ON tbl_ContractSignedEvidence(ContractId, VersionId)
        WHERE [Status] = 1;
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.6. PHÊ DUYỆT NỘI BỘ, ĐÀM PHÁN & CỔNG KHÁCH HÀNG OTP (CUSTOMER PORTAL)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ApprovalWorkflow')
BEGIN
    CREATE TABLE tbl_ApprovalWorkflow (
        WorkflowId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ApprovalWorkflow PRIMARY KEY,
        WorkflowName NVARCHAR(200) NOT NULL,
        ObjectType VARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ApprovalHistory')
BEGIN
    CREATE TABLE tbl_ApprovalHistory (
        ApprovalHistoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ApprovalHistory PRIMARY KEY,
        ObjectType VARCHAR(50) NOT NULL,
        ObjectId INT NOT NULL,
        StepNo INT NOT NULL,
        ApproverEmployeeId INT NOT NULL,
        ApprovalAction VARCHAR(30) NOT NULL,
        Comment NVARCHAR(1000) NULL,
        ActionDate DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_tbl_ApprovalHistory_Employee FOREIGN KEY (ApproverEmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ApprovalHistory_ObjectTimeline ON tbl_ApprovalHistory(ObjectType, ObjectId, ActionDate, ApprovalHistoryId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractApprovalRequest')
BEGIN
    CREATE TABLE tbl_ContractApprovalRequest (
        ApprovalRequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractApprovalRequest PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        WorkflowId INT NOT NULL,
        SubmittedByEmployeeId INT NOT NULL,
        SubmittedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_ContractApprovalRequest_SubmittedDate DEFAULT SYSUTCDATETIME(),
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_ContractApprovalRequest_Status DEFAULT 0,
        ResolvedDate DATETIME2 NULL,
        ResolvedByEmployeeId INT NULL,
        DecisionComment NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractApprovalRequest_Status CHECK ([Status] IN (0, 1, 2, 3, 4)),
        CONSTRAINT CK_tbl_ContractApprovalRequest_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractApprovalRequest_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT FK_tbl_ContractApprovalRequest_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractApprovalRequest_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId),
        CONSTRAINT FK_tbl_ContractApprovalRequest_Workflow FOREIGN KEY (WorkflowId) REFERENCES tbl_ApprovalWorkflow(WorkflowId)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_ContractApprovalRequest_PendingContract ON tbl_ContractApprovalRequest(ContractId)
        WHERE [Status] = 0;
    CREATE NONCLUSTERED INDEX IX_tbl_ContractApprovalRequest_Inbox ON tbl_ContractApprovalRequest(Status, SubmittedDate, ApprovalRequestId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractCustomerVerificationPhone')
BEGIN
    CREATE TABLE tbl_ContractCustomerVerificationPhone (
        VerificationPhoneId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractCustomerVerificationPhone PRIMARY KEY,
        ContractId INT NOT NULL,
        PhoneSource VARCHAR(32) NOT NULL,
        PhoneNumberNormalized VARCHAR(32) NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        CreatedByEmployeeId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractCustomerVerificationPhone_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractCustomerVerificationPhone_Source CHECK ([PhoneSource] IN ('CustomerMobile', 'CustomerPhone', 'Manual')),
        CONSTRAINT CK_tbl_ContractCustomerVerificationPhone_Phone CHECK (LEN(LTRIM(RTRIM([PhoneNumberNormalized]))) BETWEEN 3 AND 32),
        CONSTRAINT CK_tbl_ContractCustomerVerificationPhone_Reason CHECK (LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000),
        CONSTRAINT CK_tbl_ContractCustomerVerificationPhone_CreatedBy CHECK ([CreatedByEmployeeId] > 0)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerVerificationPhone_Contract_Chronological ON tbl_ContractCustomerVerificationPhone(ContractId, CreatedDate, VerificationPhoneId);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerVerificationPhone_Contract_Phone ON tbl_ContractCustomerVerificationPhone(ContractId, PhoneNumberNormalized);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractCustomerAccessLink')
BEGIN
    CREATE TABLE tbl_ContractCustomerAccessLink (
        CustomerAccessLinkId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractCustomerAccessLink PRIMARY KEY,
        TenantId INT NOT NULL,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        VerificationPhoneId INT NOT NULL,
        TokenHash VARCHAR(64) NOT NULL,
        CreatedByEmployeeId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ActivatedAt DATETIME2 NULL,
        ExpiresAt DATETIME2 NOT NULL,
        RevokedAt DATETIME2 NULL,
        RevokedByEmployeeId INT NULL,
        RevocationReason NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractCustomerAccessLink_LogicalIds CHECK ([TenantId] > 0 AND [ContractId] > 0 AND [VersionId] > 0 AND [VerificationPhoneId] > 0 AND [CreatedByEmployeeId] > 0),
        CONSTRAINT CK_tbl_ContractCustomerAccessLink_TokenHash CHECK (LEN(LTRIM(RTRIM([TokenHash]))) = 64),
        CONSTRAINT CK_tbl_ContractCustomerAccessLink_Expiry CHECK ([ExpiresAt] > [CreatedDate]),
        CONSTRAINT UX_tbl_ContractCustomerAccessLink_TokenHash UNIQUE (TokenHash)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_ContractCustomerAccessLink_ActiveContract ON tbl_ContractCustomerAccessLink(ContractId)
        WHERE [RevokedAt] IS NULL;
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerAccessLink_Expiry ON tbl_ContractCustomerAccessLink(ExpiresAt, RevokedAt);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractCustomerOtpChallenge')
BEGIN
    CREATE TABLE tbl_ContractCustomerOtpChallenge (
        CustomerOtpChallengeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractCustomerOtpChallenge PRIMARY KEY,
        LinkId INT NOT NULL,
        VerificationPhoneId INT NOT NULL,
        PublicChallengeId VARCHAR(64) NOT NULL,
        Purpose VARCHAR(32) NOT NULL,
        OtpHash VARCHAR(64) NOT NULL,
        FailedAttemptCount INT NOT NULL DEFAULT 0,
        ExpiresAt DATETIME2 NOT NULL,
        SentAt DATETIME2 NULL,
        UsedAt DATETIME2 NULL,
        LockedAt DATETIME2 NULL,
        InvalidatedAt DATETIME2 NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_LogicalIds CHECK ([LinkId] > 0 AND [VerificationPhoneId] > 0),
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_PublicId CHECK (LEN(LTRIM(RTRIM([PublicChallengeId]))) BETWEEN 32 AND 64),
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_Purpose CHECK ([Purpose] = 'CustomerAccess'),
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_OtpHash CHECK (LEN(LTRIM(RTRIM([OtpHash]))) = 64),
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_Attempts CHECK ([FailedAttemptCount] BETWEEN 0 AND 5),
        CONSTRAINT CK_tbl_ContractCustomerOtpChallenge_Expiry CHECK ([ExpiresAt] > [CreatedDate]),
        CONSTRAINT UX_tbl_ContractCustomerOtpChallenge_PublicChallengeId UNIQUE (PublicChallengeId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerOtpChallenge_ActiveLookup ON tbl_ContractCustomerOtpChallenge(LinkId, ExpiresAt)
        WHERE [UsedAt] IS NULL AND [LockedAt] IS NULL AND [InvalidatedAt] IS NULL;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractCustomerAccessSession')
BEGIN
    CREATE TABLE tbl_ContractCustomerAccessSession (
        CustomerAccessSessionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractCustomerAccessSession PRIMARY KEY,
        TenantId INT NOT NULL,
        LinkId INT NOT NULL,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        VerificationPhoneId INT NOT NULL,
        SessionTokenHash VARCHAR(64) NOT NULL,
        IssuedAt DATETIME2 NOT NULL,
        LastActivityAt DATETIME2 NOT NULL,
        IdleExpiresAt DATETIME2 NOT NULL,
        HardExpiresAt DATETIME2 NOT NULL,
        RevokedAt DATETIME2 NULL,
        RevocationReason NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractCustomerAccessSession_LogicalIds CHECK ([TenantId] > 0 AND [LinkId] > 0 AND [ContractId] > 0 AND [VersionId] > 0 AND [VerificationPhoneId] > 0),
        CONSTRAINT CK_tbl_ContractCustomerAccessSession_TokenHash CHECK (LEN(LTRIM(RTRIM([SessionTokenHash]))) = 64),
        CONSTRAINT CK_tbl_ContractCustomerAccessSession_Expiry CHECK ([IssuedAt] <= [LastActivityAt] AND [IdleExpiresAt] >= [LastActivityAt] AND [HardExpiresAt] >= [IdleExpiresAt]),
        CONSTRAINT UX_tbl_ContractCustomerAccessSession_TokenHash UNIQUE (SessionTokenHash)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerAccessSession_ActiveLink ON tbl_ContractCustomerAccessSession(LinkId, RevokedAt, IdleExpiresAt);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractCustomerOtpDeliveryOutbox')
BEGIN
    CREATE TABLE tbl_ContractCustomerOtpDeliveryOutbox (
        CustomerOtpDeliveryOutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractCustomerOtpDeliveryOutbox PRIMARY KEY,
        ChallengeId INT NOT NULL,
        PhoneCiphertext VARBINARY(256) NOT NULL,
        OtpCiphertext VARBINARY(128) NOT NULL,
        EmailCiphertext VARBINARY(512) NULL,
        Status VARCHAR(16) NOT NULL,
        AttemptCount INT NOT NULL DEFAULT 0,
        NextAttemptAt DATETIME2 NOT NULL,
        LeaseId VARCHAR(64) NULL,
        LeaseUntil DATETIME2 NULL,
        LastFailure NVARCHAR(1000) NULL,
        DeliveryExpiresAt DATETIME2 NOT NULL,
        SentAt DATETIME2 NULL,
        FailedAt DATETIME2 NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractCustomerOtpDeliveryOutbox_Challenge CHECK ([ChallengeId] > 0),
        CONSTRAINT CK_tbl_ContractCustomerOtpDeliveryOutbox_Status CHECK ([Status] IN ('Pending', 'Leased', 'Sent', 'Failed')),
        CONSTRAINT CK_tbl_ContractCustomerOtpDeliveryOutbox_Attempts CHECK ([AttemptCount] >= 0),
        CONSTRAINT UX_tbl_ContractCustomerOtpDeliveryOutbox_ChallengeId UNIQUE (ChallengeId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractCustomerOtpDeliveryOutbox_Lease ON tbl_ContractCustomerOtpDeliveryOutbox(Status, NextAttemptAt, LeaseUntil);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.7. CHỮ KÝ ĐIỆN TỬ TRỰC TUYẾN (E-SIGNATURE PORTAL: VẼ TAY, TỌA ĐỘ TRANG PDF & XÁC THỰC OTP)
-- Mô hình DocuSign / Base Sign / FPT eContract: Không dùng USB Token / PKI
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractElectronicSignature')
BEGIN
    CREATE TABLE tbl_ContractElectronicSignature (
        SignatureId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractElectronicSignature PRIMARY KEY,
        ContractId INT NOT NULL,                        -- Hợp đồng ký
        VersionId INT NOT NULL,                         -- Phiên bản hợp đồng ký
        PartyType TINYINT NOT NULL,                     -- 1 = Bên A (Doanh nghiệp nội bộ), 2 = Bên B (Khách hàng/Đối tác)
        SignerType VARCHAR(30) NOT NULL,                -- 'InternalEmployee', 'ExternalCustomer'
        EmployeeId INT NULL,                            -- Nếu Bên A ký: liên kết nhân viên nội bộ
        SignerName NVARCHAR(200) NOT NULL,              -- Họ và tên người ký
        SignerTitle NVARCHAR(100) NULL,                 -- Chức vụ / Đại diện
        SignerPhone VARCHAR(30) NOT NULL,               -- Số điện thoại nhận OTP
        SignerEmail VARCHAR(200) NULL,                  -- Email nhận thông báo
        SignatureType VARCHAR(30) NOT NULL CONSTRAINT DF_tbl_CES_Type DEFAULT 'DRAWN', -- 'DRAWN' (vẽ tay canvas), 'TYPED' (gõ tên), 'UPLOADED' (ảnh chữ ký), 'OTP_ACK' (xác nhận OTP)
        SignatureImageStorageKey NVARCHAR(500) NULL,    -- Đường dẫn file ảnh chữ ký nền trong suốt (PNG)
        OtpChallengeId INT NULL,                        -- FK sang tbl_ContractCustomerOtpChallenge (Bằng chứng OTP đã xác thực)
        Status TINYINT NOT NULL CONSTRAINT DF_tbl_CES_Status DEFAULT 0, -- 0 = Chờ ký (Pending), 1 = Đã ký thành công (Signed), 2 = Từ chối ký (Declined)
        SignedAt DATETIME2 NULL,                        -- Thời điểm bấm xác nhận ký
        IpAddress VARCHAR(45) NULL,                     -- Địa chỉ IP người ký (Audit trail pháp lý)
        UserAgent NVARCHAR(1000) NULL,                  -- Trình duyệt & Thiết bị người ký
        DocumentHashAtSigning VARCHAR(64) NULL,         -- Mã băm SHA-256 nội dung hợp đồng lúc ký (chống sửa đổi sau ký)
        PageNumber INT NULL,                            -- Trang đóng dấu chữ ký trên file PDF
        PositionX DECIMAL(9,2) NULL,                    -- Tọa độ X trên trang PDF
        PositionY DECIMAL(9,2) NULL,                    -- Tọa độ Y trên trang PDF
        Width DECIMAL(9,2) NULL,                        -- Kích thước khung ký
        Height DECIMAL(9,2) NULL,
        RejectReason NVARCHAR(1000) NULL,               -- Lý do từ chối nếu không đồng ý ký
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_tbl_CES_CreatedDate DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_CES_PartyType CHECK ([PartyType] IN (1, 2)),
        CONSTRAINT CK_tbl_CES_Status CHECK ([Status] IN (0, 1, 2)),
        CONSTRAINT CK_tbl_CES_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_CES_VersionId CHECK ([VersionId] > 0),
        CONSTRAINT FK_tbl_CES_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_CES_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId),
        CONSTRAINT FK_tbl_CES_Employee FOREIGN KEY (EmployeeId) REFERENCES tbl_Employee(EmployeeId),
        CONSTRAINT FK_tbl_CES_OtpChallenge FOREIGN KEY (OtpChallengeId) REFERENCES tbl_ContractCustomerOtpChallenge(CustomerOtpChallengeId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_CES_Contract_Version ON tbl_ContractElectronicSignature(ContractId, VersionId, PartyType, Status);
    CREATE NONCLUSTERED INDEX IX_tbl_CES_OtpChallenge ON tbl_ContractElectronicSignature(OtpChallengeId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractNegotiationComment')
BEGIN
    CREATE TABLE tbl_ContractNegotiationComment (
        CommentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractNegotiationComment PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        TermId INT NULL,
        ParentCommentId INT NULL,
        CarriedForwardFromCommentId INT NULL,
        CarriedForwardFromVersionId INT NULL,
        Source VARCHAR(32) NOT NULL DEFAULT 'ExternalFeedback',
        Content NVARCHAR(4000) NOT NULL,
        RecordedByEmployeeId INT NULL,
        CustomerAccessSessionId INT NULL,
        State TINYINT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractNegotiationComment_Source CHECK ([Source] IN ('ExternalFeedback', 'Customer')),
        CONSTRAINT CK_tbl_ContractNegotiationComment_State CHECK ([State] IN (0, 1)),
        CONSTRAINT FK_tbl_ContractNegotiationComment_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractNegotiationComment_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractNegotiationComment_Version_Chronological ON tbl_ContractNegotiationComment(ContractId, VersionId, CreatedDate, CommentId);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractNegotiationComment_ParentCommentId ON tbl_ContractNegotiationComment(ParentCommentId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractNegotiationCommentEvent')
BEGIN
    CREATE TABLE tbl_ContractNegotiationCommentEvent (
        CommentEventId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractNegotiationCommentEvent PRIMARY KEY,
        CommentId INT NOT NULL,
        EventType INT NOT NULL,
        ActorType VARCHAR(32) NOT NULL DEFAULT 'Employee',
        EmployeeId INT NULL,
        CustomerAccessSessionId INT NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_tbl_ContractNegotiationCommentEvent_CommentId CHECK ([CommentId] > 0),
        CONSTRAINT CK_tbl_ContractNegotiationCommentEvent_EventType CHECK ([EventType] IN (1, 2, 3, 4)),
        CONSTRAINT FK_tbl_ContractNegotiationCommentEvent_Comment FOREIGN KEY (CommentId) REFERENCES tbl_ContractNegotiationComment(CommentId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractNegotiationCommentEvent_Chronological ON tbl_ContractNegotiationCommentEvent(CommentId, OccurredAt, CommentEventId);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.7. SNAPSHOT PHÁP LÝ BẤT BIẾN KHI KHÓA HỢP ĐỒNG (CONTRACT IMMUTABLE SNAPSHOTS)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractVersionLegalSnapshot')
BEGIN
    CREATE TABLE tbl_ContractVersionLegalSnapshot (
        ContractVersionLegalSnapshotId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractVersionLegalSnapshot PRIMARY KEY,
        VersionId INT NOT NULL,
        ContractId INT NOT NULL,
        VersionNo INT NOT NULL,
        ContractDirection INT NOT NULL DEFAULT 1,
        TemplateVersionId INT NOT NULL,
        ContractCode VARCHAR(50) NULL,
        ContractName NVARCHAR(1000) NOT NULL,
        ContractNameEn VARCHAR(500) NULL,
        CurrencyCode CHAR(3) NOT NULL,
        Subtotal DECIMAL(18,2) NOT NULL,
        TotalDiscount DECIMAL(18,2) NOT NULL,
        TotalVat DECIMAL(18,2) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        ContractCreatedDate DATETIME2 NOT NULL,
        SignDate DATETIME2 NULL,
        EffectiveDate DATETIME2 NULL,
        ExpireDate DATETIME2 NULL,
        CreatedByEmployeeId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        CONSTRAINT CK_tbl_ContractVersionLegalSnapshot_Ids CHECK ([VersionId] > 0 AND [ContractId] > 0 AND [VersionNo] > 0 AND [TemplateVersionId] > 0 AND [CreatedByEmployeeId] > 0),
        CONSTRAINT CK_tbl_ContractVersionLegalSnapshot_Financials CHECK ([Subtotal] >= 0 AND [TotalDiscount] >= 0 AND [TotalVat] >= 0 AND [TotalAmount] >= 0),
        CONSTRAINT CK_tbl_ContractVersionLegalSnapshot_ContractDirection CHECK ([ContractDirection] IN (1, 2)),
        CONSTRAINT UX_tbl_ContractVersionLegalSnapshot_VersionId UNIQUE (VersionId),
        CONSTRAINT FK_tbl_ContractVersionLegalSnapshot_Version FOREIGN KEY (VersionId) REFERENCES tbl_ContractVersion(VersionId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractVersionPartySnapshot')
BEGIN
    CREATE TABLE tbl_ContractVersionPartySnapshot (
        ContractVersionPartySnapshotId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractVersionPartySnapshot PRIMARY KEY,
        ContractVersionLegalSnapshotId BIGINT NOT NULL,
        PartyRole TINYINT NOT NULL,
        SourceCustomerId INT NULL,
        SubjectCode NVARCHAR(100) NULL,
        FullName NVARCHAR(300) NULL,
        LegalName NVARCHAR(1000) NOT NULL,
        LegalNameEn NVARCHAR(1000) NULL,
        TaxCode VARCHAR(50) NULL,
        Address NVARCHAR(2000) NULL,
        AddressEn NVARCHAR(2000) NULL,
        City NVARCHAR(100) NULL,
        CityEn NVARCHAR(100) NULL,
        Country NVARCHAR(100) NULL,
        CountryEn NVARCHAR(100) NULL,
        ZipCode NVARCHAR(50) NULL,
        RepresentativeName NVARCHAR(500) NULL,
        RepresentativeTitle NVARCHAR(500) NULL,
        RepresentativeTitleEn NVARCHAR(500) NULL,
        ContactName NVARCHAR(300) NULL,
        ContactTitle NVARCHAR(300) NULL,
        ContactTitleEn NVARCHAR(500) NULL,
        ContactPhone NVARCHAR(100) NULL,
        PhoneNumber VARCHAR(50) NULL,
        Mobile NVARCHAR(100) NULL,
        FaxNumber VARCHAR(50) NULL,
        Email NVARCHAR(255) NULL,
        Website NVARCHAR(500) NULL,
        BankAccountNumber VARCHAR(100) NULL,
        BankName NVARCHAR(500) NULL,
        BankNameEn NVARCHAR(1000) NULL,
        CONSTRAINT CK_tbl_ContractVersionPartySnapshot_Role CHECK ([PartyRole] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractVersionPartySnapshot_Source CHECK (([PartyRole] = 1 AND [SourceCustomerId] IS NULL) OR ([PartyRole] = 2 AND [SourceCustomerId] > 0)),
        CONSTRAINT UX_tbl_ContractVersionPartySnapshot_Legal_Role UNIQUE (ContractVersionLegalSnapshotId, PartyRole),
        CONSTRAINT FK_tbl_ContractVersionPartySnapshot_Legal FOREIGN KEY (ContractVersionLegalSnapshotId) REFERENCES tbl_ContractVersionLegalSnapshot(ContractVersionLegalSnapshotId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractVersionPaymentMilestoneSnapshot')
BEGIN
    CREATE TABLE tbl_ContractVersionPaymentMilestoneSnapshot (
        ContractVersionPaymentMilestoneSnapshotId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractVersionPaymentMilestoneSnapshot PRIMARY KEY,
        ContractVersionLegalSnapshotId BIGINT NOT NULL,
        SourcePaymentMilestoneId INT NOT NULL,
        SourceTermId INT NOT NULL,
        SourceTemplatePaymentMilestoneId INT NULL,
        MilestoneCode VARCHAR(100) NOT NULL,
        TitleVi NVARCHAR(500) NOT NULL,
        TitleEn VARCHAR(500) NULL,
        PaymentPercent DECIMAL(9,4) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        DueOffsetDays INT NOT NULL,
        DueAnchor TINYINT NOT NULL,
        DayCountMode TINYINT NOT NULL,
        AnchorDate DATETIME2 NULL,
        DueDate DATETIME2 NULL,
        PaymentStatus TINYINT NOT NULL,
        PaidAt DATETIME2 NULL,
        PaidByEmployeeId INT NULL,
        ConditionVi NVARCHAR(2000) NULL,
        ConditionEn VARCHAR(2000) NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT CK_tbl_ContractVersionPaymentMilestoneSnapshot_Ids CHECK ([SourcePaymentMilestoneId] > 0 AND [SourceTermId] > 0 AND ([SourceTemplatePaymentMilestoneId] IS NULL OR [SourceTemplatePaymentMilestoneId] > 0)),
        CONSTRAINT CK_tbl_ContractVersionPaymentMilestoneSnapshot_Values CHECK ([PaymentPercent] > 0 AND [PaymentPercent] <= 100 AND [Amount] >= 0 AND [DisplayOrder] > 0),
        CONSTRAINT UX_tbl_ContractVersionPaymentMilestoneSnapshot_Legal_Milestone UNIQUE (ContractVersionLegalSnapshotId, SourcePaymentMilestoneId),
        CONSTRAINT FK_tbl_ContractVersionPaymentMilestoneSnapshot_Legal FOREIGN KEY (ContractVersionLegalSnapshotId) REFERENCES tbl_ContractVersionLegalSnapshot(ContractVersionLegalSnapshotId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractVersionPaymentMilestoneSnapshot_Legal_Order ON tbl_ContractVersionPaymentMilestoneSnapshot(ContractVersionLegalSnapshotId, DisplayOrder);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractVersionPlaceholderValue')
BEGIN
    CREATE TABLE tbl_ContractVersionPlaceholderValue (
        ContractVersionPlaceholderValueId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractVersionPlaceholderValue PRIMARY KEY,
        ContractId INT NOT NULL,
        VersionId INT NOT NULL,
        PlaceholderKey VARCHAR(100) NOT NULL,
        SourceFieldKey VARCHAR(200) NOT NULL,
        RenderedValue NVARCHAR(MAX) NULL,
        CONSTRAINT UX_tbl_ContractVersionPlaceholderValue_Version_Key UNIQUE (VersionId, PlaceholderKey)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractVersionPlaceholderValue_Contract_Version ON tbl_ContractVersionPlaceholderValue(ContractId, VersionId);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.8. VẬN HÀNH BÀN GIAO & NGHIỆM THU (HANDOVER & ACCEPTANCE MODULES)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverBaseline')
BEGIN
    CREATE TABLE tbl_ContractHandoverBaseline (
        HandoverBaselineId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverBaseline PRIMARY KEY,
        ContractId INT NOT NULL,
        ContractVersionId INT NOT NULL,
        ConfirmedByEmployeeId INT NOT NULL,
        ConfirmedAt DATETIME2 NOT NULL,
        CONSTRAINT UX_tbl_ContractHandoverBaseline_Contract_Version UNIQUE (ContractId, ContractVersionId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverScopeItem')
BEGIN
    CREATE TABLE tbl_ContractHandoverScopeItem (
        HandoverScopeItemId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverScopeItem PRIMARY KEY,
        HandoverBaselineId BIGINT NOT NULL,
        SourceContractItemId INT NOT NULL,
        ScopeMode TINYINT NOT NULL,
        ItemCode NVARCHAR(100) NOT NULL,
        NameVi NVARCHAR(2000) NOT NULL,
        NameEn NVARCHAR(2000) NULL,
        UnitVi NVARCHAR(100) NOT NULL,
        UnitEn NVARCHAR(100) NULL,
        SourceQuantity DECIMAL(18,4) NOT NULL,
        RequiredQuantity DECIMAL(18,4) NOT NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT CK_HandoverScope_Mode CHECK ([ScopeMode] IN (1, 2)),
        CONSTRAINT CK_HandoverScope_Quantity CHECK ([SourceQuantity] > 0 AND [RequiredQuantity] > 0 AND (([ScopeMode] = 1 AND [RequiredQuantity] = [SourceQuantity]) OR ([ScopeMode] = 2 AND [RequiredQuantity] = 1))),
        CONSTRAINT UX_tbl_ContractHandoverScopeItem_Baseline_Source UNIQUE (HandoverBaselineId, SourceContractItemId),
        CONSTRAINT UX_tbl_ContractHandoverScopeItem_Baseline_Order UNIQUE (HandoverBaselineId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractHandoverScopeItem_Baseline FOREIGN KEY (HandoverBaselineId) REFERENCES tbl_ContractHandoverBaseline(HandoverBaselineId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverRecord')
BEGIN
    CREATE TABLE tbl_ContractHandoverRecord (
        HandoverRecordId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverRecord PRIMARY KEY,
        ContractId INT NOT NULL,
        ContractVersionId INT NOT NULL,
        HandoverBaselineId BIGINT NOT NULL,
        TemplateVersionId INT NOT NULL,
        HandoverCode NVARCHAR(100) NOT NULL,
        HandoverDate DATE NOT NULL,
        ContractSignDate DATE NOT NULL,
        Location NVARCHAR(500) NOT NULL,
        LocationEn NVARCHAR(500) NULL,
        Status TINYINT NOT NULL DEFAULT 0,
        SnapshotHash VARCHAR(64) NULL,
        CreatedEmployeeId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedEmployeeId INT NULL,
        UpdatedAt DATETIME2 NULL,
        FinalizedByEmployeeId INT NULL,
        FinalizedAt DATETIME2 NULL,
        SignedByEmployeeId INT NULL,
        SignedAt DATETIME2 NULL,
        CancelledByEmployeeId INT NULL,
        CancelledAt DATETIME2 NULL,
        CancellationReason NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_HandoverRecord_Status CHECK ([Status] IN (0, 1, 2, 3)),
        CONSTRAINT CK_HandoverRecord_Hash CHECK (([SnapshotHash] IS NULL OR LEN([SnapshotHash]) = 64) AND ([Status] NOT IN (1, 2) OR [SnapshotHash] IS NOT NULL)),
        CONSTRAINT UX_tbl_ContractHandoverRecord_Contract_Code UNIQUE (ContractId, HandoverCode),
        CONSTRAINT FK_tbl_ContractHandoverRecord_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractHandoverRecord_Baseline FOREIGN KEY (HandoverBaselineId) REFERENCES tbl_ContractHandoverBaseline(HandoverBaselineId)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_HandoverRecord_ActiveVersion ON tbl_ContractHandoverRecord(ContractId, ContractVersionId)
        WHERE [Status] IN (0, 1);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractHandoverRecord_Version_Status ON tbl_ContractHandoverRecord(ContractVersionId, Status);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverLine')
BEGIN
    CREATE TABLE tbl_ContractHandoverLine (
        HandoverLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverLine PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        HandoverScopeItemId BIGINT NOT NULL,
        Quantity DECIMAL(18,4) NOT NULL,
        DisplayOrder INT NOT NULL,
        NotesVi NVARCHAR(2000) NULL,
        NotesEn NVARCHAR(2000) NULL,
        CONSTRAINT CK_HandoverLine_Quantity CHECK ([Quantity] > 0),
        CONSTRAINT UX_tbl_ContractHandoverLine_Record_Scope UNIQUE (HandoverRecordId, HandoverScopeItemId),
        CONSTRAINT UX_tbl_ContractHandoverLine_Record_Order UNIQUE (HandoverRecordId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractHandoverLine_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractHandoverLine_Scope FOREIGN KEY (HandoverScopeItemId) REFERENCES tbl_ContractHandoverScopeItem(HandoverScopeItemId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractHandoverLine_Scope ON tbl_ContractHandoverLine(HandoverScopeItemId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverParty')
BEGIN
    CREATE TABLE tbl_ContractHandoverParty (
        HandoverPartyId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverParty PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        SubjectKind TINYINT NOT NULL,
        LegalName NVARCHAR(1000) NOT NULL,
        LegalNameEn NVARCHAR(1000) NULL,
        Address NVARCHAR(2000) NULL,
        AddressEn NVARCHAR(2000) NULL,
        Phone NVARCHAR(100) NULL,
        Fax NVARCHAR(100) NULL,
        TaxCode NVARCHAR(50) NULL,
        RepresentativeName NVARCHAR(300) NOT NULL,
        RepresentativeTitle NVARCHAR(500) NULL,
        RepresentativeTitleEn NVARCHAR(500) NULL,
        CONSTRAINT CK_HandoverParty_Subject CHECK ([SubjectKind] IN (1, 2)),
        CONSTRAINT UX_tbl_ContractHandoverParty_Record_Subject UNIQUE (HandoverRecordId, SubjectKind),
        CONSTRAINT FK_tbl_ContractHandoverParty_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverReference')
BEGIN
    CREATE TABLE tbl_ContractHandoverReference (
        HandoverReferenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverReference PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        AppendixId INT NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT UX_tbl_ContractHandoverReference_Record_Order UNIQUE (HandoverRecordId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractHandoverReference_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverSection')
BEGIN
    CREATE TABLE tbl_ContractHandoverSection (
        HandoverSectionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverSection PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        SectionCode NVARCHAR(100) NOT NULL,
        TitleVi NVARCHAR(1000) NOT NULL,
        TitleEn NVARCHAR(1000) NULL,
        ContentVi NVARCHAR(MAX) NULL,
        ContentEn NVARCHAR(MAX) NULL,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL,
        CONSTRAINT CK_HandoverSection_Mode CHECK ([BilingualContentMode] IN (1, 2)),
        CONSTRAINT UX_tbl_ContractHandoverSection_Record_Code UNIQUE (HandoverRecordId, SectionCode),
        CONSTRAINT UX_tbl_ContractHandoverSection_Record_Order UNIQUE (HandoverRecordId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractHandoverSection_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverEvidence')
BEGIN
    CREATE TABLE tbl_ContractHandoverEvidence (
        HandoverEvidenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverEvidence PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        FileId INT NOT NULL,
        SignDate DATE NOT NULL,
        UploadedByEmployeeId INT NOT NULL,
        UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_tbl_ContractHandoverEvidence_Record UNIQUE (HandoverRecordId),
        CONSTRAINT UX_tbl_ContractHandoverEvidence_File UNIQUE (FileId),
        CONSTRAINT FK_tbl_ContractHandoverEvidence_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractHandoverEvidence_File FOREIGN KEY (FileId) REFERENCES tbl_FileStorage(FileId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractHandoverPlaceholderValue')
BEGIN
    CREATE TABLE tbl_ContractHandoverPlaceholderValue (
        HandoverPlaceholderValueId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractHandoverPlaceholderValue PRIMARY KEY,
        HandoverRecordId INT NOT NULL,
        PlaceholderKey NVARCHAR(100) NOT NULL,
        SourceFieldKey NVARCHAR(250) NOT NULL,
        RenderedValue NVARCHAR(MAX) NOT NULL,
        CONSTRAINT UX_tbl_ContractHandoverPlaceholderValue_Record_Key UNIQUE (HandoverRecordId, PlaceholderKey),
        CONSTRAINT FK_tbl_ContractHandoverPlaceholderValue_Record FOREIGN KEY (HandoverRecordId) REFERENCES tbl_ContractHandoverRecord(HandoverRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceRecord')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceRecord (
        AcceptanceRecordId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceRecord PRIMARY KEY,
        ContractId INT NOT NULL,
        ContractVersionId INT NOT NULL,
        TemplateVersionId INT NOT NULL,
        AcceptanceCode VARCHAR(100) NOT NULL,
        AcceptanceDate DATE NOT NULL,
        ContractSignDate DATE NOT NULL,
        Location NVARCHAR(500) NOT NULL,
        LocationEn NVARCHAR(500) NULL,
        AcceptanceKind TINYINT NOT NULL DEFAULT 1,
        Status TINYINT NOT NULL DEFAULT 0,
        SnapshotHash CHAR(64) NULL,
        CancellationReason NVARCHAR(1000) NULL,
        CreatedEmployeeId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedEmployeeId INT NULL,
        UpdatedDate DATETIME2 NULL,
        FinalizedByEmployeeId INT NULL,
        FinalizedAt DATETIME2 NULL,
        SignedByEmployeeId INT NULL,
        SignedAt DATETIME2 NULL,
        CancelledByEmployeeId INT NULL,
        CancelledAt DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractAcceptanceRecord_Kind CHECK ([AcceptanceKind] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractAcceptanceRecord_Status CHECK ([Status] IN (0, 1, 2, 3)),
        CONSTRAINT CK_tbl_ContractAcceptanceRecord_Hash CHECK ([SnapshotHash] IS NULL OR LEN([SnapshotHash]) = 64),
        CONSTRAINT UX_tbl_ContractAcceptanceRecord_Contract_Code UNIQUE (ContractId, AcceptanceCode),
        CONSTRAINT FK_tbl_ContractAcceptanceRecord_Contract FOREIGN KEY (ContractId) REFERENCES tbl_Contract(ContractId),
        CONSTRAINT FK_tbl_ContractAcceptanceRecord_Version FOREIGN KEY (ContractVersionId) REFERENCES tbl_ContractVersion(VersionId)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_tbl_ContractAcceptanceRecord_ActiveVersion ON tbl_ContractAcceptanceRecord(ContractVersionId)
        WHERE [Status] IN (0, 1);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAcceptanceRecord_Version_Status ON tbl_ContractAcceptanceRecord(ContractVersionId, Status);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceScopeAllocation')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceScopeAllocation (
        AcceptanceScopeAllocationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceScopeAllocation PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        HandoverLineId INT NOT NULL,
        Quantity DECIMAL(18,4) NOT NULL,
        CONSTRAINT CK_AcceptanceScopeAllocation_Quantity CHECK ([Quantity] > 0),
        CONSTRAINT UX_tbl_ContractAcceptanceScopeAllocation_Record_Line UNIQUE (AcceptanceRecordId, HandoverLineId),
        CONSTRAINT FK_tbl_ContractAcceptanceScopeAllocation_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractAcceptanceScopeAllocation_Line FOREIGN KEY (HandoverLineId) REFERENCES tbl_ContractHandoverLine(HandoverLineId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAcceptanceScopeAllocation_Line ON tbl_ContractAcceptanceScopeAllocation(HandoverLineId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceReference')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceReference (
        AcceptanceReferenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceReference PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        ReferenceType TINYINT NOT NULL,
        AppendixId INT NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT CK_tbl_ContractAcceptanceReference_Type CHECK ([ReferenceType] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractAcceptanceReference_Target CHECK (([ReferenceType] = 1 AND [AppendixId] IS NULL) OR ([ReferenceType] = 2 AND [AppendixId] IS NOT NULL)),
        CONSTRAINT UX_tbl_ContractAcceptanceReference_Record_Order UNIQUE (AcceptanceRecordId, DisplayOrder),
        CONSTRAINT UX_tbl_ContractAcceptanceReference_Record_Type_Appendix UNIQUE (AcceptanceRecordId, ReferenceType, AppendixId),
        CONSTRAINT FK_tbl_ContractAcceptanceReference_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractAcceptanceReference_Appendix FOREIGN KEY (AppendixId) REFERENCES tbl_ContractAppendix(AppendixId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceParty')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceParty (
        AcceptancePartyId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceParty PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        PartyRole TINYINT NOT NULL,
        LegalName NVARCHAR(500) NOT NULL,
        LegalNameEn NVARCHAR(500) NULL,
        Address NVARCHAR(1000) NULL,
        AddressEn NVARCHAR(2000) NULL,
        Phone NVARCHAR(50) NULL,
        Fax NVARCHAR(50) NULL,
        TaxCode VARCHAR(50) NULL,
        RepresentativeName NVARCHAR(300) NOT NULL,
        RepresentativeTitle NVARCHAR(300) NULL,
        RepresentativeTitleEn NVARCHAR(300) NULL,
        CONSTRAINT CK_tbl_ContractAcceptanceParty_Role CHECK ([PartyRole] IN (1, 2)),
        CONSTRAINT UX_tbl_ContractAcceptanceParty_Record_Role UNIQUE (AcceptanceRecordId, PartyRole),
        CONSTRAINT FK_tbl_ContractAcceptanceParty_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceSection')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceSection (
        AcceptanceSectionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceSection PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        SectionCode VARCHAR(100) NOT NULL,
        TitleVi NVARCHAR(1000) NOT NULL,
        TitleEn NVARCHAR(1000) NULL,
        ContentVi NVARCHAR(MAX) NULL,
        ContentEn NVARCHAR(MAX) NULL,
        BilingualContentMode TINYINT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractAcceptanceSection_Order CHECK ([DisplayOrder] >= 0),
        CONSTRAINT CK_TblContractAcceptanceSection_BilingualContentMode CHECK ([BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [ContentEn] IS NULL)),
        CONSTRAINT UX_tbl_ContractAcceptanceSection_Record_Code UNIQUE (AcceptanceRecordId, SectionCode),
        CONSTRAINT UX_tbl_ContractAcceptanceSection_Record_Order UNIQUE (AcceptanceRecordId, DisplayOrder),
        CONSTRAINT FK_tbl_ContractAcceptanceSection_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceMilestone')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceMilestone (
        AcceptanceMilestoneId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceMilestone PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        PaymentMilestoneId INT NOT NULL,
        CONSTRAINT UX_tbl_ContractAcceptanceMilestone_Record_Milestone UNIQUE (AcceptanceRecordId, PaymentMilestoneId),
        CONSTRAINT FK_tbl_ContractAcceptanceMilestone_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractAcceptanceMilestone_Milestone FOREIGN KEY (PaymentMilestoneId) REFERENCES tbl_ContractPaymentMilestone(PaymentMilestoneId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAcceptanceEvidence')
BEGIN
    CREATE TABLE tbl_ContractAcceptanceEvidence (
        AcceptanceEvidenceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAcceptanceEvidence PRIMARY KEY,
        AcceptanceRecordId INT NOT NULL,
        FileId INT NOT NULL,
        SignDate DATE NOT NULL,
        UploadedByEmployeeId INT NOT NULL,
        UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractAcceptanceEvidence_RecordId CHECK ([AcceptanceRecordId] > 0),
        CONSTRAINT CK_tbl_ContractAcceptanceEvidence_FileId CHECK ([FileId] > 0),
        CONSTRAINT UX_tbl_ContractAcceptanceEvidence_File UNIQUE (FileId),
        CONSTRAINT UX_tbl_ContractAcceptanceEvidence_Record UNIQUE (AcceptanceRecordId),
        CONSTRAINT FK_tbl_ContractAcceptanceEvidence_Record FOREIGN KEY (AcceptanceRecordId) REFERENCES tbl_ContractAcceptanceRecord(AcceptanceRecordId),
        CONSTRAINT FK_tbl_ContractAcceptanceEvidence_File FOREIGN KEY (FileId) REFERENCES tbl_FileStorage(FileId)
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.9. AUDIT LOG & THÔNG BÁO THỜI GIAN THỰC (CONTRACT AUDIT & NOTIFICATION SUBSYSTEM)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAudit')
BEGIN
    CREATE TABLE tbl_ContractAudit (
        ContractAuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAudit PRIMARY KEY,
        TenantId INT NOT NULL,
        ContractId INT NOT NULL,
        VersionId INT NULL,
        SubjectType VARCHAR(64) NULL,
        SubjectId INT NULL,
        ActorType VARCHAR(32) NOT NULL,
        ActorEmployeeId INT NULL,
        ActorCustomerAccessSessionId INT NULL,
        ActionType VARCHAR(64) NOT NULL,
        ActorDisplayNameSnapshot NVARCHAR(1000) NULL,
        ActorMaskedPhoneSnapshot VARCHAR(32) NULL,
        ActorPhoneSourceSnapshot VARCHAR(32) NULL,
        ContractCodeSnapshot VARCHAR(50) NULL,
        ContractNameSnapshot NVARCHAR(1000) NULL,
        VersionNoSnapshot INT NULL,
        PreviousResponsibleEmployeeId INT NULL,
        NewResponsibleEmployeeId INT NULL,
        Result VARCHAR(32) NOT NULL,
        Reason NVARCHAR(1000) NULL,
        FailureCode VARCHAR(64) NULL,
        OccurredAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IpAddress VARCHAR(45) NULL,
        UserAgent NVARCHAR(1024) NULL,
        CorrelationId VARCHAR(100) NOT NULL,
        CONSTRAINT CK_tbl_ContractAudit_TenantId CHECK ([TenantId] > 0),
        CONSTRAINT CK_tbl_ContractAudit_ContractId CHECK ([ContractId] > 0),
        CONSTRAINT CK_tbl_ContractAudit_ActorType CHECK (LEN(LTRIM(RTRIM([ActorType]))) > 0),
        CONSTRAINT CK_tbl_ContractAudit_ActionType CHECK (LEN(LTRIM(RTRIM([ActionType]))) > 0),
        CONSTRAINT CK_tbl_ContractAudit_Result CHECK (LEN(LTRIM(RTRIM([Result]))) > 0),
        CONSTRAINT CK_tbl_ContractAudit_CorrelationId CHECK (LEN(LTRIM(RTRIM([CorrelationId]))) > 0)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAudit_Tenant_Contract_Occurred ON tbl_ContractAudit(TenantId, ContractId, OccurredAt);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAudit_Tenant_Occurred_Id ON tbl_ContractAudit(TenantId, OccurredAt DESC, ContractAuditId DESC);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAudit_Tenant_Subject_Occurred ON tbl_ContractAudit(TenantId, SubjectType, SubjectId, OccurredAt DESC, ContractAuditId DESC);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAudit_Tenant_CorrelationId ON tbl_ContractAudit(TenantId, CorrelationId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractAuditValue')
BEGIN
    CREATE TABLE tbl_ContractAuditValue (
        ContractAuditValueId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractAuditValue PRIMARY KEY,
        ContractAuditId BIGINT NOT NULL,
        ValueSide TINYINT NOT NULL,
        FieldCode INT NOT NULL,
        ValueKind TINYINT NOT NULL,
        IsNull BIT NOT NULL,
        IntegerValue INT NULL,
        DecimalValue DECIMAL(19,4) NULL,
        StringValue NVARCHAR(500) NULL,
        DateTimeValue DATETIME2 NULL,
        BooleanValue BIT NULL,
        CONSTRAINT CK_tbl_ContractAuditValue_Side CHECK ([ValueSide] IN (1, 2)),
        CONSTRAINT CK_tbl_ContractAuditValue_FieldCode CHECK ([FieldCode] BETWEEN 1 AND 103),
        CONSTRAINT CK_tbl_ContractAuditValue_ValueKind CHECK ([ValueKind] IN (1, 2, 3, 4, 5)),
        CONSTRAINT UX_tbl_ContractAuditValue_Audit_Side_Field UNIQUE (ContractAuditId, ValueSide, FieldCode),
        CONSTRAINT FK_tbl_ContractAuditValue_Audit FOREIGN KEY (ContractAuditId) REFERENCES tbl_ContractAudit(ContractAuditId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractAuditValue_Audit_Side ON tbl_ContractAuditValue(ContractAuditId, ValueSide);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_Notification')
BEGIN
    CREATE TABLE tbl_Notification (
        NotificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_Notification PRIMARY KEY,
        EventKey VARCHAR(200) NOT NULL,
        ModuleCode VARCHAR(50) NOT NULL,
        EventCode VARCHAR(80) NOT NULL,
        SubjectType VARCHAR(50) NOT NULL,
        SubjectId INT NOT NULL,
        Title NVARCHAR(300) NOT NULL,
        Body NVARCHAR(1000) NOT NULL,
        ActionKey VARCHAR(80) NULL,
        ActorEmployeeId INT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_tbl_Notification_EventKey CHECK (LEN(LTRIM(RTRIM([EventKey]))) > 0),
        CONSTRAINT CK_tbl_Notification_SubjectId CHECK ([SubjectId] > 0),
        CONSTRAINT UX_tbl_Notification_EventKey UNIQUE (EventKey),
        CONSTRAINT FK_tbl_Notification_Actor FOREIGN KEY (ActorEmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_NotificationRecipient')
BEGIN
    CREATE TABLE tbl_NotificationRecipient (
        NotificationRecipientId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_NotificationRecipient PRIMARY KEY,
        NotificationId BIGINT NOT NULL,
        EmployeeId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ReadAtUtc DATETIME2 NULL,
        DismissedAtUtc DATETIME2 NULL,
        CONSTRAINT UX_tbl_NotificationRecipient_Notif_Emp UNIQUE (NotificationId, EmployeeId),
        CONSTRAINT FK_tbl_NotificationRecipient_Notif FOREIGN KEY (NotificationId) REFERENCES tbl_Notification(NotificationId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_NotificationRecipient_Employee FOREIGN KEY (EmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_NotificationRecipient_Emp_Timeline ON tbl_NotificationRecipient(EmployeeId, CreatedAtUtc DESC, NotificationRecipientId DESC);
    CREATE NONCLUSTERED INDEX IX_tbl_NotificationRecipient_Emp_Read ON tbl_NotificationRecipient(EmployeeId, ReadAtUtc);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_NotificationDispatchOutbox')
BEGIN
    CREATE TABLE tbl_NotificationDispatchOutbox (
        OutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_NotificationDispatchOutbox PRIMARY KEY,
        NotificationId BIGINT NOT NULL,
        Status VARCHAR(16) NOT NULL,
        AttemptCount INT NOT NULL DEFAULT 0,
        NextAttemptAtUtc DATETIME2 NOT NULL,
        LeaseId UNIQUEIDENTIFIER NULL,
        LeaseUntilUtc DATETIME2 NULL,
        LastErrorCode VARCHAR(80) NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DispatchedAtUtc DATETIME2 NULL,
        CONSTRAINT CK_tbl_NotificationDispatchOutbox_Status CHECK ([Status] IN ('Pending', 'Leased', 'Dispatched', 'Failed')),
        CONSTRAINT CK_tbl_NotificationDispatchOutbox_AttemptCount CHECK ([AttemptCount] >= 0),
        CONSTRAINT UX_tbl_NotificationDispatchOutbox_NotifId UNIQUE (NotificationId),
        CONSTRAINT FK_tbl_NotificationDispatchOutbox_Notif FOREIGN KEY (NotificationId) REFERENCES tbl_Notification(NotificationId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_NotificationDispatchOutbox_Status_NextAttempt ON tbl_NotificationDispatchOutbox(Status, NextAttemptAtUtc);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.10. SỐ HÓA HỢP ĐỒNG BẢN CỨNG BẰNG MÁY SCAN / APP MOBILE & OCR BÓC TÁCH KEYWORDS [HƯỚNG 2]
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractScanBatch')
BEGIN
    CREATE TABLE tbl_ContractScanBatch (
        BatchId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractScanBatch PRIMARY KEY,
        BatchCode VARCHAR(50) NOT NULL UNIQUE,
        ScanSource VARCHAR(30) NOT NULL,
        DeviceId NVARCHAR(200) NULL,
        TotalDocuments INT NOT NULL DEFAULT 1,
        TotalPages INT NOT NULL DEFAULT 0,
        Status VARCHAR(30) NOT NULL DEFAULT 'Uploaded',
        CreatedByEmployeeId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CompletedAt DATETIME2 NULL,
        Notes NVARCHAR(1000) NULL,
        CONSTRAINT CK_tbl_ContractScanBatch_Source CHECK (ScanSource IN ('Scanner', 'MobileApp', 'ImageUpload')),
        CONSTRAINT CK_tbl_ContractScanBatch_Status CHECK (Status IN ('Uploaded', 'Processing', 'Completed', 'Failed')),
        CONSTRAINT FK_tbl_ContractScanBatch_Employee FOREIGN KEY (CreatedByEmployeeId) REFERENCES tbl_Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractScanDocument')
BEGIN
    CREATE TABLE tbl_ContractScanDocument (
        ScanDocumentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractScanDocument PRIMARY KEY,
        BatchId INT NOT NULL,
        DocumentTitle NVARCHAR(500) NOT NULL,
        DocumentTypeCode VARCHAR(50) NULL,
        OriginalFileName NVARCHAR(300) NULL,
        MergedPdfFileId INT NULL,
        TotalPages INT NOT NULL DEFAULT 1,
        OcrStatus VARCHAR(30) NOT NULL DEFAULT 'Pending',
        VerificationStatus VARCHAR(30) NOT NULL DEFAULT 'Unverified',
        VerifiedByEmployeeId INT NULL,
        VerifiedAt DATETIME2 NULL,
        AssociatedContractId INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_tbl_ContractScanDocument_OcrStatus CHECK (OcrStatus IN ('Pending', 'Processing', 'Completed', 'NeedsReview', 'Failed')),
        CONSTRAINT CK_tbl_ContractScanDocument_VerificationStatus CHECK (VerificationStatus IN ('Unverified', 'Verified', 'Rejected')),
        CONSTRAINT FK_tbl_ContractScanDocument_Batch FOREIGN KEY (BatchId) REFERENCES tbl_ContractScanBatch(BatchId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractScanDocument_MergedPdf FOREIGN KEY (MergedPdfFileId) REFERENCES tbl_FileStorage(FileId),
        CONSTRAINT FK_tbl_ContractScanDocument_Verifier FOREIGN KEY (VerifiedByEmployeeId) REFERENCES tbl_Employee(EmployeeId),
        CONSTRAINT FK_tbl_ContractScanDocument_Contract FOREIGN KEY (AssociatedContractId) REFERENCES tbl_Contract(ContractId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractScanDocument_Batch ON tbl_ContractScanDocument(BatchId);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractScanDocument_OcrStatus ON tbl_ContractScanDocument(OcrStatus);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractScanDocument_Contract ON tbl_ContractScanDocument(AssociatedContractId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractScanPage')
BEGIN
    CREATE TABLE tbl_ContractScanPage (
        ScanPageId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractScanPage PRIMARY KEY,
        ScanDocumentId INT NOT NULL,
        PageNumber INT NOT NULL,
        ImageFileId INT NOT NULL,
        DpiResolution INT NOT NULL DEFAULT 300,
        SkewAngle DECIMAL(6,2) NULL,
        IsRotated BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_tbl_ContractScanPage_Doc_Page UNIQUE (ScanDocumentId, PageNumber),
        CONSTRAINT FK_tbl_ContractScanPage_Doc FOREIGN KEY (ScanDocumentId) REFERENCES tbl_ContractScanDocument(ScanDocumentId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractScanPage_ImageFile FOREIGN KEY (ImageFileId) REFERENCES tbl_FileStorage(FileId)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractOcrResult')
BEGIN
    CREATE TABLE tbl_ContractOcrResult (
        OcrResultId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractOcrResult PRIMARY KEY,
        ScanDocumentId INT NOT NULL,
        ScanPageId BIGINT NULL,
        OcrEngine VARCHAR(50) NOT NULL DEFAULT 'ConTrack_Tesseract_Azure',
        FullTextContent NVARCHAR(MAX) NOT NULL,
        LayoutJson NVARCHAR(MAX) NULL,
        AverageConfidence DECIMAL(5,2) NOT NULL DEFAULT 0,
        ProcessingDurationMs INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_tbl_ContractOcrResult_Doc FOREIGN KEY (ScanDocumentId) REFERENCES tbl_ContractScanDocument(ScanDocumentId) ON DELETE CASCADE,
        CONSTRAINT FK_tbl_ContractOcrResult_Page FOREIGN KEY (ScanPageId) REFERENCES tbl_ContractScanPage(ScanPageId)
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractOcrResult_Doc ON tbl_ContractOcrResult(ScanDocumentId);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractOcrExtractedKey')
BEGIN
    CREATE TABLE tbl_ContractOcrExtractedKey (
        ExtractedKeyId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractOcrExtractedKey PRIMARY KEY,
        ScanDocumentId INT NOT NULL,
        KeyType VARCHAR(50) NOT NULL,
        KeyLabel NVARCHAR(200) NOT NULL,
        ExtractedValue NVARCHAR(2000) NOT NULL,
        NormalizedValue NVARCHAR(1000) NULL,
        ConfidenceScore DECIMAL(5,2) NOT NULL,
        BoundingBoxJson NVARCHAR(500) NULL,
        IsManuallyEdited BIT NOT NULL DEFAULT 0,
        UserEditedValue NVARCHAR(2000) NULL,
        FinalValue AS (ISNULL(UserEditedValue, ExtractedValue)),
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_tbl_ContractOcrExtractedKey_Confidence CHECK (ConfidenceScore BETWEEN 0 AND 100),
        CONSTRAINT FK_tbl_ContractOcrExtractedKey_Doc FOREIGN KEY (ScanDocumentId) REFERENCES tbl_ContractScanDocument(ScanDocumentId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractOcrExtractedKey_Doc ON tbl_ContractOcrExtractedKey(ScanDocumentId);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractOcrExtractedKey_KeyType ON tbl_ContractOcrExtractedKey(KeyType);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_ContractOcrSearchIndex')
BEGIN
    CREATE TABLE tbl_ContractOcrSearchIndex (
        SearchIndexId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tbl_ContractOcrSearchIndex PRIMARY KEY,
        ScanDocumentId INT NOT NULL,
        Keyword NVARCHAR(200) NOT NULL,
        PageNumber INT NOT NULL,
        OccurrencesCount INT NOT NULL DEFAULT 1,
        SnippetContext NVARCHAR(1000) NULL,
        CONSTRAINT FK_tbl_ContractOcrSearchIndex_Doc FOREIGN KEY (ScanDocumentId) REFERENCES tbl_ContractScanDocument(ScanDocumentId) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_tbl_ContractOcrSearchIndex_Keyword ON tbl_ContractOcrSearchIndex(Keyword);
    CREATE NONCLUSTERED INDEX IX_tbl_ContractOcrSearchIndex_Doc ON tbl_ContractOcrSearchIndex(ScanDocumentId);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- 2.11. STORED PROCEDURE ĐỒNG BỘ TỪ BẢN SCAN ĐÃ VERIFY SANG HỢP ĐỒNG SỐ CHÍNH THỨC
-- ----------------------------------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE sp_SyncVerifiedOcrKeysToContract
    @ScanDocumentId INT,
    @EmployeeId INT,
    @NewContractId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- 1. Kiểm tra trạng thái tài liệu scan
        DECLARE @VerifStatus VARCHAR(30), @BatchId INT, @Title NVARCHAR(500), @PdfFileId INT;
        SELECT 
            @VerifStatus = VerificationStatus,
            @BatchId = BatchId,
            @Title = DocumentTitle,
            @PdfFileId = MergedPdfFileId
        FROM tbl_ContractScanDocument
        WHERE ScanDocumentId = @ScanDocumentId;

        IF @VerifStatus <> 'Verified'
        BEGIN
            RAISERROR (N'Chỉ tài liệu scan đã được kiểm tra và duyệt (Verified) mới được đồng bộ vào Hợp đồng số!', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 2. Đọc các key đã trích xuất & chuẩn hóa
        DECLARE @ContractCode VARCHAR(50) = NULL;
        DECLARE @PartnerName NVARCHAR(500) = NULL;
        DECLARE @TaxCode VARCHAR(50) = NULL;
        DECLARE @SignDate DATETIME2 = NULL;
        DECLARE @EffectiveDate DATETIME2 = NULL;
        DECLARE @ExpireDate DATETIME2 = NULL;
        DECLARE @TotalAmount DECIMAL(18,2) = 0;

        SELECT @ContractCode = FinalValue FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'ContractCode';
        SELECT @PartnerName = FinalValue FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'PartnerName';
        SELECT @TaxCode = FinalValue FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'TaxCode';
        SELECT @TotalAmount = TRY_CAST(FinalValue AS DECIMAL(18,2)) FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'TotalAmount';
        SELECT @SignDate = TRY_CAST(FinalValue AS DATETIME2) FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'SignDate';
        SELECT @EffectiveDate = TRY_CAST(FinalValue AS DATETIME2) FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'EffectiveDate';
        SELECT @ExpireDate = TRY_CAST(FinalValue AS DATETIME2) FROM tbl_ContractOcrExtractedKey WHERE ScanDocumentId = @ScanDocumentId AND KeyType = 'ExpireDate';

        -- 3. Tìm hoặc Tạo mới Khách hàng / Đối tác (tbl_Customers)
        DECLARE @CustomerId INT = NULL;
        IF @TaxCode IS NOT NULL AND LEN(LTRIM(RTRIM(@TaxCode))) > 0
        BEGIN
            SELECT TOP 1 @CustomerId = CustomerId FROM tbl_Customers WHERE CustomerTaxCode = @TaxCode;
        END;

        IF @CustomerId IS NULL AND @PartnerName IS NOT NULL
        BEGIN
            SELECT TOP 1 @CustomerId = CustomerId FROM tbl_Customers WHERE CustomerCompany = @PartnerName OR CustomerFullName = @PartnerName;
        END;

        IF @CustomerId IS NULL
        BEGIN
            INSERT INTO tbl_Customers (CustomerFullName, CustomerCompany, CustomerTaxCode, DateCreated, Status)
            VALUES (ISNULL(@PartnerName, N'Đối tác scan mới'), @PartnerName, @TaxCode, GETDATE(), 1);
            SET @CustomerId = SCOPE_IDENTITY();
        END;

        -- 4. Lấy template mặc định cho hợp đồng quét (hoặc template đầu tiên active)
        DECLARE @TemplateVersionId INT = NULL;
        SELECT TOP 1 @TemplateVersionId = CurrentPublishedVersionId
        FROM tbl_ContractTemplate
        WHERE IsActive = 1 AND CurrentPublishedVersionId IS NOT NULL
        ORDER BY TemplateId ASC;

        IF @TemplateVersionId IS NULL
        BEGIN
            SELECT TOP 1 @TemplateVersionId = TemplateVersionId
            FROM tbl_ContractTemplateVersion
            ORDER BY TemplateVersionId ASC;
        END;

        -- 5. Tạo bản ghi Hợp đồng (tbl_Contract)
        INSERT INTO tbl_Contract (
            ContractCode,
            ContractName,
            CustomerId,
            EmployeeId,
            TemplateVersionId,
            ContractType,
            ContractDirection,
            LanguageMode,
            Status,
            TotalAmount,
            Subtotal,
            SignDate,
            EffectiveDate,
            ExpireDate,
            CreatedDate
        )
        VALUES (
            @ContractCode,
            @Title,
            @CustomerId,
            @EmployeeId,
            ISNULL(@TemplateVersionId, 1),
            1,
            1,
            1,
            5,
            ISNULL(@TotalAmount, 0),
            ISNULL(@TotalAmount, 0),
            @SignDate,
            @EffectiveDate,
            @ExpireDate,
            SYSUTCDATETIME()
        );
        SET @NewContractId = SCOPE_IDENTITY();

        -- 6. Tạo Version 1 đã khóa (tbl_ContractVersion)
        DECLARE @VersionId INT;
        INSERT INTO tbl_ContractVersion (
            ContractId,
            VersionNo,
            TemplateVersionId,
            ChangeNote,
            Subtotal,
            TotalAmount,
            IsLocked,
            LockedDate,
            LockedByEmployeeId,
            SnapshotHash,
            CreatedDate
        )
        VALUES (
            @NewContractId,
            1,
            ISNULL(@TemplateVersionId, 1),
            N'Hợp đồng được số hóa tự động từ bản cứng quét máy/app OCR',
            ISNULL(@TotalAmount, 0),
            ISNULL(@TotalAmount, 0),
            1,
            SYSUTCDATETIME(),
            @EmployeeId,
            REPLICATE('0', 64),
            SYSUTCDATETIME()
        );
        SET @VersionId = SCOPE_IDENTITY();

        UPDATE tbl_Contract
        SET CurrentVersionId = @VersionId
        WHERE ContractId = @NewContractId;

        -- 7. Nếu có File scan PDF, tự động gắn vào Signed Evidence (tbl_ContractSignedEvidence)
        IF @PdfFileId IS NOT NULL
        BEGIN
            INSERT INTO tbl_ContractSignedEvidence (
                ContractId,
                VersionId,
                FileId,
                Status,
                SignDate,
                UploadedByEmployeeId,
                UploadedAt
            )
            VALUES (
                @NewContractId,
                @VersionId,
                @PdfFileId,
                1,
                ISNULL(@SignDate, CAST(GETDATE() AS DATE)),
                @EmployeeId,
                SYSUTCDATETIME()
            );
        END;

        -- 8. Cập nhật liên kết ngược vào tbl_ContractScanDocument
        UPDATE tbl_ContractScanDocument
        SET AssociatedContractId = @NewContractId
        WHERE ScanDocumentId = @ScanDocumentId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

PRINT N'====================================================================================';
PRINT N'ĐÃ HOÀN TẤT KHỞI TẠO VÀ CẬP NHẬT DATABASE CONTRACK (CENTRAL DB & TENANT DB) THÀNH CÔNG!';
PRINT N'CHUẨN HÓA 100% THEO SƠ ĐỒ ERD ẢNH VÀ TƯƠNG THÍCH MÃ NGUỒN C# .NET EF CORE!';
PRINT N'====================================================================================';
