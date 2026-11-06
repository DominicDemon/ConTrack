/* =====================================================================================
   SEED DATA CHO DATABASE: MasterDB (MasterDB-Optimize_v1.sql)
   - Có thể chạy lại nhiều lần (Idempotent: kiểm tra từng bản ghi trước khi INSERT/UPDATE)
   - Mật khẩu mặc định cho tất cả tài khoản EMPLOYEE / CUSTOMER: 123456aA45$%
   ===================================================================================== */

USE [MasterDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DefaultPasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxAzPX7zO3S/k6l2cP8qipbqXU9ql4+4uka6Dxa0p+t20w==';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @NowDt DATETIME = GETUTCDATE();

BEGIN TRANSACTION;

-- =====================================================================================
-- 1. CATEGORIES & PRODUCTS
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [CATEGORIES] WHERE [category_id] = 1)
BEGIN
    SET IDENTITY_INSERT [CATEGORIES] ON;
    INSERT INTO [CATEGORIES] ([category_id], [category_name], [category_short_desc], [category_desc], [category_image], [category_parentid], [category_sortorder])
    VALUES
        (1, N'Phần mềm quản lý doanh nghiệp', N'Giải pháp SaaS quản trị hợp đồng và vận hành', N'Danh mục phần mềm quản trị doanh nghiệp', 'cat-enterprise.png', NULL, 1),
        (2, N'Giải pháp chữ ký số & Lưu trữ', N'Tích hợp ký số và lưu trữ tài liệu đám mây', N'Danh mục giải pháp ký số', 'cat-esign.png', NULL, 2);
    SET IDENTITY_INSERT [CATEGORIES] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [PRODUCTS] WHERE [product_code] = 'CONTRACK-PRO')
BEGIN
    SET IDENTITY_INSERT [PRODUCTS] ON;
    INSERT INTO [PRODUCTS] (
        [product_id], [product_code], [product_name], [product_nameen], [product_shortname],
        [product_unitname], [product_unitnameen], [product_slogan], [product_shortdesc],
        [product_details], [category_id], [product_suggested_price], [product_quantity],
        [product_sortorder], [product_created_at]
    )
    VALUES
        (1, 'CONTRACK-PRO', N'Phần mềm Quản lý Hợp đồng ConTrack Pro', N'ConTrack Pro Contract Management', N'ConTrack Pro',
         N'Gói/Năm', N'Package/Year', N'Quản lý vòng đời hợp đồng toàn diện', N'Nền tảng quản trị hợp đồng điện tử cho doanh nghiệp',
         N'Hỗ trợ tạo mẫu, phê duyệt, đàm phán, ký số và lưu trữ hợp đồng.', 1, 2880000, 100, 1, @NowDt),
        (2, 'CONTRACK-OCR', N'Module Quét & Bóc tách Hợp đồng AI OCR', N'ConTrack AI OCR Module', N'ConTrack OCR',
         N'Gói', N'Package', N'Số hóa hợp đồng giấy nhanh chóng', N'Module nhận diện văn bản hợp đồng tự động',
         N'Trích xuất dữ liệu tự động từ file PDF/Scan.', 1, 1500000, 100, 2, @NowDt);
    SET IDENTITY_INSERT [PRODUCTS] OFF;
END;

-- =====================================================================================
-- 2. SERVICE_TYPES & SERVICES
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [SERVICE_TYPES] WHERE [servicetype_id] = 1)
BEGIN
    SET IDENTITY_INSERT [SERVICE_TYPES] ON;
    INSERT INTO [SERVICE_TYPES] ([servicetype_id], [servicetype_name])
    VALUES
        (1, N'Dịch vụ Triển khai & Đào tạo'),
        (2, N'Dịch vụ Hỗ trợ Kỹ thuật & Bảo trì SLA');
    SET IDENTITY_INSERT [SERVICE_TYPES] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [SERVICES] WHERE [service_id] = 1)
BEGIN
    SET IDENTITY_INSERT [SERVICES] ON;
    INSERT INTO [SERVICES] (
        [service_id], [service_name], [service_nameen], [service_unitname], [service_unitnameen],
        [service_price], [servicetype_id], [service_region], [service_haschild],
        [service_status], [service_datecreated]
    )
    VALUES
        (1, N'Triển khai & Chuẩn hóa Mẫu hợp đồng', N'Contract Template Onboarding', N'Lần', N'One-time', 5000000, 1, 1, 0, 1, @NowDt),
        (2, N'Gói Hỗ trợ Kỹ thuật Ưu tiên 24/7', N'24/7 Priority Technical Support', N'Năm', N'Year', 3600000, 2, 1, 0, 1, @NowDt);
    SET IDENTITY_INSERT [SERVICES] OFF;
END;

-- =====================================================================================
-- 3. DEPARTMENT (Phòng ban)
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [DEPARTMENT] WHERE [department_code] = 'BGD')
BEGIN
    SET IDENTITY_INSERT [DEPARTMENT] ON;
    INSERT INTO [DEPARTMENT] ([department_id], [department_code], [department_name], [department_description], [department_status], [created_at])
    VALUES
        (1, 'BGD',    N'Ban Giám Đốc',                 N'Ban điều hành và phê duyệt cấp cao', 'ACTIVE', @Now),
        (2, 'KTHUAT', N'Phòng Kỹ Thuật & Hỗ Trợ',       N'Phòng xử lý kỹ thuật và hỗ trợ khách hàng (Support Ticket)', 'ACTIVE', @Now),
        (3, 'KD',     N'Phòng Kinh Doanh',             N'Phòng tư vấn và chăm sóc hợp đồng khách hàng', 'ACTIVE', @Now),
        (4, 'KTTC',   N'Phòng Kế Toán - Tài Chính',    N'Phòng quản lý hóa đơn, thanh toán và công nợ', 'ACTIVE', @Now),
        (5, 'HCNS',   N'Phòng Hành Chính - Pháp Chế',  N'Phòng kiểm soát pháp lý và quản trị nội bộ', 'ACTIVE', @Now);
    SET IDENTITY_INSERT [DEPARTMENT] OFF;
END;

-- =====================================================================================
-- 4. EMPLOYEE (Kiểm tra từng tài khoản độc lập để không bị bỏ qua khi sysadmin đã tồn tại)
--    Mật khẩu mặc định: 123456aA45$%
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'sysadmin' OR [employee_code] = 'ADM-SYSADMIN')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('ADM-SYSADMIN', 'Me@gmail.com', '0901000001', @DefaultPasswordHash, N'BossVipPro (System Admin)', 'sysadmin', '001099000001', 'ACTIVE', '1990-01-01', 'MALE', N'Hà Nội', @Now, @Now, @Now);
END
ELSE
BEGIN
    UPDATE [EMPLOYEE]
    SET [employee_password_hash] = @DefaultPasswordHash,
        [employee_personal_identifier] = ISNULL([employee_personal_identifier], '001099000001'),
        [employee_phone] = ISNULL([employee_phone], '0901000001'),
        [employee_status] = 'ACTIVE'
    WHERE [employee_username] = 'sysadmin';
END;

-- Đảm bảo không có bản ghi EMPLOYEE nào bị NULL ở cột UNIQUE (employee_personal_identifier, employee_phone)
UPDATE [EMPLOYEE]
SET [employee_personal_identifier] = 'ID-' + CAST([employee_id] AS VARCHAR(15))
WHERE [employee_personal_identifier] IS NULL;

UPDATE [EMPLOYEE]
SET [employee_phone] = '0900' + RIGHT('000000' + CAST([employee_id] AS VARCHAR(10)), 6)
WHERE [employee_phone] IS NULL;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.manager' OR [employee_code] = 'EMP-MANAGER')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('EMP-MANAGER', 'manager@contrack.vn', '0901000002', @DefaultPasswordHash, N'Nguyễn Văn Quản Lý', 'test.manager', '001099000002', 'ACTIVE', '1992-05-10', 'MALE', N'Hà Nội', @Now, @Now, @Now);
END
ELSE
BEGIN
    UPDATE [EMPLOYEE]
    SET [employee_password_hash] = @DefaultPasswordHash,
        [employee_status] = 'ACTIVE'
    WHERE [employee_username] = 'test.manager';
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.technical' OR [employee_code] = 'EMP-TECH01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('EMP-TECH01', 'hungnguyennn119@gmail.com', '0901000003', @DefaultPasswordHash, N'Trần Kỹ Thuật (Support Lead)', 'test.technical', '001099000003', 'ACTIVE', '1996-08-15', 'MALE', N'Hà Nội', @Now, @Now, @Now);
END
ELSE
BEGIN
    UPDATE [EMPLOYEE]
    SET [employee_password_hash] = @DefaultPasswordHash,
        [employee_status] = 'ACTIVE'
    WHERE [employee_username] = 'test.technical';
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.sale' OR [employee_code] = 'EMP-SALE01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('EMP-SALE01', 'sale@contrack.vn', '0901000004', @DefaultPasswordHash, N'Lê Thị Kinh Doanh', 'test.sale', '001099000004', 'ACTIVE', '1997-03-20', 'FEMALE', N'TP.HCM', @Now, @Now, @Now);
END
ELSE
BEGIN
    UPDATE [EMPLOYEE]
    SET [employee_password_hash] = @DefaultPasswordHash,
        [employee_status] = 'ACTIVE'
    WHERE [employee_username] = 'test.sale';
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.accountant' OR [employee_code] = 'EMP-ACC01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('EMP-ACC01', 'accountant@contrack.vn', '0901000005', @DefaultPasswordHash, N'Phạm Kế Toán', 'test.accountant', '001099000005', 'ACTIVE', '1995-11-12', 'FEMALE', N'Hà Nội', @Now, @Now, @Now);
END
ELSE
BEGIN
    UPDATE [EMPLOYEE]
    SET [employee_password_hash] = @DefaultPasswordHash,
        [employee_status] = 'ACTIVE'
    WHERE [employee_username] = 'test.accountant';
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = N'Nhat-Bố' OR [employee_code] = 'NNN' OR [employee_email] = 'nhatvlhe180831@fpt.edu.vn')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('NNN', 'nhatvlhe180831@fpt.edu.vn', '0396912529', @DefaultPasswordHash, N'Nhật bố của các loại bố', N'Nhat-Bố', '001099000006', 'ACTIVE', '2004-01-01', 'MALE', N'Hà Nội', @Now, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.sale' OR [employee_code] = 'HEAD-SALE01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('HEAD-SALE01', 'head.sale@contrack.vn', '0901000013', @DefaultPasswordHash, N'Hoàng Trưởng Phòng Kinh Doanh', 'head.sale', '001099000013', 'ACTIVE', '1989-06-15', 'MALE', N'TP.HCM', @Now, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.accountant' OR [employee_code] = 'HEAD-ACC01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('HEAD-ACC01', 'head.accountant@contrack.vn', '0901000014', @DefaultPasswordHash, N'Đặng Trưởng Phòng Kế Toán', 'head.accountant', '001099000014', 'ACTIVE', '1988-09-20', 'FEMALE', N'Hà Nội', @Now, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'head.legal' OR [employee_code] = 'HEAD-LEGAL01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('HEAD-LEGAL01', 'head.legal@contrack.vn', '0901000015', @DefaultPasswordHash, N'Vũ Trưởng Phòng Hành Chính - Pháp Chế', 'head.legal', '001099000015', 'ACTIVE', '1987-12-10', 'MALE', N'Hà Nội', @Now, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'test.legal' OR [employee_code] = 'EMP-LEGAL01')
BEGIN
    INSERT INTO [EMPLOYEE] (
        [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES ('EMP-LEGAL01', 'legal@contrack.vn', '0901000016', @DefaultPasswordHash, N'Bùi Chuyên Viên Pháp Chế', 'test.legal', '001099000016', 'ACTIVE', '1996-04-18', 'FEMALE', N'Hà Nội', @Now, @Now, @Now);
END;

DECLARE @EmpSysId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'sysadmin');
DECLARE @EmpMgrId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.manager');
DECLARE @EmpTechId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.technical');
DECLARE @EmpHeadSaleId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.sale');
DECLARE @EmpSaleId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.sale');
DECLARE @EmpHeadAccId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.accountant');
DECLARE @EmpAccId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.accountant');
DECLARE @EmpHeadLegalId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'head.legal');
DECLARE @EmpLegalId BIGINT = (SELECT TOP 1 [employee_id] FROM [EMPLOYEE] WHERE [employee_username] = 'test.legal');

IF @EmpSysId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpSysId AND [department_id] = 1)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpSysId, 1, @Now, @EmpSysId);
IF @EmpMgrId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpMgrId AND [department_id] = 2)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpMgrId, 2, @Now, @EmpSysId);
IF @EmpTechId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpTechId AND [department_id] = 2)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpTechId, 2, @Now, @EmpSysId);
IF @EmpHeadSaleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpHeadSaleId AND [department_id] = 3)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpHeadSaleId, 3, @Now, @EmpSysId);
IF @EmpSaleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpSaleId AND [department_id] = 3)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpSaleId, 3, @Now, @EmpSysId);
IF @EmpHeadAccId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpHeadAccId AND [department_id] = 4)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpHeadAccId, 4, @Now, @EmpSysId);
IF @EmpAccId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpAccId AND [department_id] = 4)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpAccId, 4, @Now, @EmpSysId);
IF @EmpHeadLegalId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpHeadLegalId AND [department_id] = 5)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpHeadLegalId, 5, @Now, @EmpSysId);
IF @EmpLegalId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = @EmpLegalId AND [department_id] = 5)
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by]) VALUES (@EmpLegalId, 5, @Now, @EmpSysId);

-- =====================================================================================
-- 5. CUSTOMER & CUSTOMER_BUSINESS (Khách hàng - Mật khẩu: 123456aA45$%)
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [CUSTOMER] WHERE [customer_email] = 'contact@abc-corp.vn')
BEGIN
    SET IDENTITY_INSERT [CUSTOMER] ON;
    INSERT INTO [CUSTOMER] (
        [customer_id], [customer_email], [customer_personal_phone], [customer_password_hash],
        [customer_display_name], [customer_personal_tax_identifier], [customer_status],
        [customer_dob], [customer_gender], [customer_address], [created_at], [created_by]
    )
    VALUES
        (1, 'contact@abc-corp.vn',   '0912345001', @DefaultPasswordHash, N'Công ty Cổ phần Công nghệ ABC',     '0101234567', 1, '1988-04-12', 'MALE',   N'Tầng 12, Tòa nhà Keangnam, Nam Từ Liêm, Hà Nội', @Now, @EmpSysId),
        (2, 'info@minhphat-group.vn','0912345002', @DefaultPasswordHash, N'Công ty TNHH Thương mại Minh Phát', '0309876543', 1, '1991-09-25', 'FEMALE', N'Số 45 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh',     @Now, @EmpSysId),
        (3, 'support@vietstar.vn',   '0912345003', @DefaultPasswordHash, N'Tập đoàn Đầu tư Sao Việt',          '0105556668', 1, '1985-12-01', 'MALE',   N'Số 88 Láng Hạ, Đống Đa, Hà Nội',                @Now, @EmpSysId);
    SET IDENTITY_INSERT [CUSTOMER] OFF;
END
ELSE
BEGIN
    UPDATE [CUSTOMER]
    SET [customer_password_hash] = @DefaultPasswordHash,
        [customer_status] = 1
    WHERE [customer_email] IN ('contact@abc-corp.vn', 'info@minhphat-group.vn', 'support@vietstar.vn')
       OR [customer_password_hash] IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM [CUSTOMER_BUSINESS] WHERE [customer_id] = 1)
BEGIN
    SET IDENTITY_INSERT [CUSTOMER_BUSINESS] ON;
    INSERT INTO [CUSTOMER_BUSINESS] (
        [customer_business_id], [customer_id], [customer_business_legal_name],
        [customer_business_company_tax_identifier], [customer_business_phone],
        [customer_business_email], [customer_business_office_address],
        [customer_business_head_office_address], [customer_business_type],
        [customer_business_representative_name], [created_at], [created_by]
    )
    VALUES
        (1, 1, N'CÔNG TY CỔ PHẦN CÔNG NGHỆ ABC',     '0101234567', '0243888999', 'contact@abc-corp.vn',    N'Nam Từ Liêm, Hà Nội', N'Nam Từ Liêm, Hà Nội', 'JSC', N'Nguyễn Thành Nam', @Now, @EmpSysId),
        (2, 2, N'CÔNG TY TNHH THƯƠNG MẠI MINH PHÁT', '0309876543', '0283777888', 'info@minhphat-group.vn', N'Quận 1, TP.HCM',      N'Quận 1, TP.HCM',      'LLC', N'Trần Minh Phát',   @Now, @EmpSysId);
    SET IDENTITY_INSERT [CUSTOMER_BUSINESS] OFF;
END;

-- =====================================================================================
-- 6. SAAS_TYPE, PACKAGE_LEVEL, PLAN, PLAN_VERSION, TENANT, TENANT_DATABASE
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [SAAS_TYPE] WHERE [saas_type_code] = 'CONTRACK')
BEGIN
    SET IDENTITY_INSERT [SAAS_TYPE] ON;
    INSERT INTO [SAAS_TYPE] ([saas_type_id], [product_id], [saas_type_code], [saas_type_name], [saas_type_description], [saas_type_status], [created_at])
    VALUES (1, 1, 'CONTRACK', N'ConTrack SaaS', N'Phần mềm quản lý hợp đồng thông minh', 'ACTIVE', @Now);
    SET IDENTITY_INSERT [SAAS_TYPE] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [PACKAGE_LEVEL] WHERE [package_level_id] = 1)
BEGIN
    SET IDENTITY_INSERT [PACKAGE_LEVEL] ON;
    INSERT INTO [PACKAGE_LEVEL] ([package_level_id], [saas_type_id], [package_level_code], [package_level_name], [package_level_description], [package_level_status], [created_at])
    VALUES
        (1, 1, 'TRIAL',    N'Gói Dùng Thử',   N'Gói trải nghiệm 15 ngày', 'ACTIVE', @Now),
        (2, 1, 'STANDARD', N'Gói Tiêu Chuẩn', N'Gói đầy đủ tính năng và hỗ trợ Ticket', 'ACTIVE', @Now);
    SET IDENTITY_INSERT [PACKAGE_LEVEL] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [PLAN] WHERE [plan_id] = 1)
BEGIN
    SET IDENTITY_INSERT [PLAN] ON;
    INSERT INTO [PLAN] ([plan_id], [saas_type_id], [package_level_id], [plan_code], [plan_name], [plan_status], [created_at])
    VALUES
        (1, 1, 1, 'TRIAL',    N'Gói Dùng thử 15 ngày', 'ACTIVE', @Now),
        (2, 1, 2, 'STANDARD', N'Gói Tiêu chuẩn',       'ACTIVE', @Now);
    SET IDENTITY_INSERT [PLAN] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [PLAN_VERSION] WHERE [plan_version_id] = 1)
BEGIN
    SET IDENTITY_INSERT [PLAN_VERSION] ON;
    INSERT INTO [PLAN_VERSION] (
        [plan_version_id], [plan_id], [plan_version_number], [plan_version_duration_days],
        [plan_version_price], [plan_version_currency], [plan_version_effective_from],
        [plan_version_status], [created_at]
    )
    VALUES
        (1, 1, 1, 15,  0,       'VND', '2026-01-01', 'ACTIVE', @Now),
        (2, 2, 1, 90,  900000,  'VND', '2026-01-01', 'ACTIVE', @Now),
        (3, 2, 2, 180, 1620000, 'VND', '2026-01-01', 'ACTIVE', @Now),
        (4, 2, 3, 365, 2880000, 'VND', '2026-01-01', 'ACTIVE', @Now);
    SET IDENTITY_INSERT [PLAN_VERSION] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TENANT] WHERE [tenant_code] = 'dtc')
BEGIN
    INSERT INTO [TENANT] (
        [customer_id], [saas_type_id], [package_level_id],
        [tenant_code], [tenant_name], [tenant_subdomain], [tenant_status],
        [tenant_expired_at], [activated_at], [created_at]
    )
    VALUES (1, 1, 2, 'dtc', N'DTC Technology (Default Tenant)', 'dtc', 'ACTIVE', DATEADD(YEAR, 1, @Now), @NowDt, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [TENANT] WHERE [tenant_code] = 'minhphat')
BEGIN
    INSERT INTO [TENANT] (
        [customer_id], [saas_type_id], [package_level_id],
        [tenant_code], [tenant_name], [tenant_subdomain], [tenant_status],
        [tenant_expired_at], [activated_at], [created_at]
    )
    VALUES (2, 1, 2, 'minhphat', N'Công ty TNHH Minh Phát', 'minhphat', 'ACTIVE', DATEADD(YEAR, 1, @Now), @NowDt, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [TENANT] WHERE [tenant_code] = 'NNN-tenant')
BEGIN
    INSERT INTO [TENANT] (
        [customer_id], [saas_type_id], [package_level_id],
        [tenant_code], [tenant_name], [tenant_subdomain], [tenant_status],
        [tenant_expired_at], [activated_at], [created_at]
    )
    VALUES (1, 1, 2, 'NNN-tenant', N'Công ty TNHH 1 mình tao', 'nnn-tenant', 'ACTIVE', DATEADD(YEAR, 1, @Now), @NowDt, @Now);
END;

DECLARE @TenantDtcId BIGINT = (SELECT TOP 1 [tenant_id] FROM [TENANT] WHERE [tenant_code] = 'dtc');
DECLARE @TenantMpId BIGINT = (SELECT TOP 1 [tenant_id] FROM [TENANT] WHERE [tenant_code] = 'minhphat');
DECLARE @TenantNnnId BIGINT = (SELECT TOP 1 [tenant_id] FROM [TENANT] WHERE [tenant_code] = 'NNN-tenant');

IF @TenantDtcId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TENANT_DATABASE] WHERE [tenant_id] = @TenantDtcId)
BEGIN
    INSERT INTO [TENANT_DATABASE] (
        [tenant_id], [tenant_database_provider],
        [tenant_database_host], [tenant_database_port], [tenant_database_db_name],
        [tenant_database_user], [tenant_database_provisioning_status], [created_at]
    )
    VALUES (@TenantDtcId, 'SqlServer', 'localhost', 1433, 'MasterDB', 'sa', 'READY', @Now);
END;

IF @TenantMpId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TENANT_DATABASE] WHERE [tenant_id] = @TenantMpId)
BEGIN
    INSERT INTO [TENANT_DATABASE] (
        [tenant_id], [tenant_database_provider],
        [tenant_database_host], [tenant_database_port], [tenant_database_db_name],
        [tenant_database_user], [tenant_database_provisioning_status], [created_at]
    )
    VALUES (@TenantMpId, 'SqlServer', 'localhost', 1433, 'MasterDB', 'sa', 'READY', @Now);
END;

IF @TenantNnnId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TENANT_DATABASE] WHERE [tenant_id] = @TenantNnnId)
BEGIN
    INSERT INTO [TENANT_DATABASE] (
        [tenant_id], [tenant_database_provider],
        [tenant_database_host], [tenant_database_port], [tenant_database_db_name],
        [tenant_database_user], [tenant_database_provisioning_status], [created_at]
    )
    VALUES (@TenantNnnId, 'SqlServer', 'localhost', 1433, 'MasterDB', 'sa', 'READY', @Now);
END;

-- =====================================================================================
-- 7. TICKET_TYPES, TICKET_SUPPORT, TICKET_SUPPORT_EMPLOYEE, TICKET_MESSAGE, TICKET_HISTORY
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [TICKET_TYPES] WHERE [ticket_type_id] = 1)
BEGIN
    SET IDENTITY_INSERT [TICKET_TYPES] ON;
    INSERT INTO [TICKET_TYPES] ([ticket_type_id], [ticket_name])
    VALUES
        (1, N'Hỗ trợ kỹ thuật & Lỗi hệ thống'),
        (2, N'Tư vấn nghiệp vụ hợp đồng & Mẫu biểu'),
        (3, N'Thanh toán, Gia hạn & Hóa đơn'),
        (4, N'Yêu cầu phân quyền & Tài khoản');
    SET IDENTITY_INSERT [TICKET_TYPES] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TICKET_SUPPORT] WHERE [ticket_code] = 'TK-2026-0001')
BEGIN
    SET IDENTITY_INSERT [TICKET_SUPPORT] ON;
    INSERT INTO [TICKET_SUPPORT] (
        [ticket_support_id], [customer_id], [department_id], [tenant_id],
        [product_id], [service_id], [ticket_code], [ticket_title],
        [ticket_description], [ticket_priority], [ticket_status],
        [ticket_resolution], [ticket_resolved_at], [ticket_closed_at],
        [created_at], [updated_at]
    )
    VALUES
        (1, 1, 2, @TenantDtcId, 1, 2, 'TK-2026-0001',
         N'Không xuất được file PDF hợp đồng song ngữ',
         N'Khi nhấn xem trước PDF ở hợp đồng mẫu song ngữ Việt - Anh thì hệ thống báo lỗi timeout.',
         'HIGH', 'IN_PROGRESS',
         NULL, NULL, NULL,
         DATEADD(HOUR, -5, @NowDt), DATEADD(HOUR, -2, @NowDt)),

        (2, 2, 3, @TenantMpId, 1, 1, 'TK-2026-0002',
         N'Hỗ trợ cấu hình trường dữ liệu động (Placeholder) cho phụ lục',
         N'Nhờ bộ phận hỗ trợ hướng dẫn cách thêm bảng danh mục hàng hóa tự động vào mẫu phụ lục hợp đồng.',
         'MEDIUM', 'OPEN',
         NULL, NULL, NULL,
         DATEADD(HOUR, -3, @NowDt), DATEADD(HOUR, -3, @NowDt)),

        (3, 1, 4, @TenantDtcId, 2, NULL, 'TK-2026-0003',
         N'Yêu cầu xuất hóa đơn VAT cho gói Tiêu chuẩn 1 năm',
         N'Công ty ABC đã thanh toán thành công đơn hàng gia hạn 12 tháng, nhờ kế toán xuất hóa đơn điện tử.',
         'LOW', 'RESOLVED',
         N'Đã phát hành hóa đơn điện tử và gửi qua email contact@abc-corp.vn.',
         DATEADD(HOUR, -1, @NowDt), NULL,
         DATEADD(DAY, -1, @NowDt), DATEADD(HOUR, -1, @NowDt));
    SET IDENTITY_INSERT [TICKET_SUPPORT] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TICKET_SUPPORT_EMPLOYEE] WHERE [ticket_support_id] = 1)
BEGIN
    SET IDENTITY_INSERT [TICKET_SUPPORT_EMPLOYEE] ON;
    INSERT INTO [TICKET_SUPPORT_EMPLOYEE] (
        [ticket_support_employee_id], [ticket_support_id], [employee_id],
        [ticket_support_employee_assignment_role], [ticket_support_employee_assigned_at],
        [ticket_support_employee_unassigned_at], [ticket_support_employee_assigned_by_employee_id]
    )
    VALUES
        (1, 1, ISNULL(@EmpTechId, 1), 'PRIMARY', DATEADD(HOUR, -4, @NowDt), NULL, ISNULL(@EmpMgrId, 1)),
        (2, 3, ISNULL(@EmpAccId, 1),  'PRIMARY', DATEADD(HOUR, -12, @NowDt), NULL, ISNULL(@EmpMgrId, 1));
    SET IDENTITY_INSERT [TICKET_SUPPORT_EMPLOYEE] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TICKET_MESSAGE] WHERE [ticket_support_id] = 1)
BEGIN
    SET IDENTITY_INSERT [TICKET_MESSAGE] ON;
    INSERT INTO [TICKET_MESSAGE] (
        [ticket_message_id], [ticket_support_id], [ticket_message_sender_customer_id],
        [ticket_message_sender_employee_id], [ticket_message_content],
        [ticket_message_type], [created_at]
    )
    VALUES
        (1, 1, 1, NULL, N'Chào đội ngũ hỗ trợ, mình xuất PDF hợp đồng số HĐ-2026/01 bị xoay vòng khá lâu rồi báo lỗi.', 'CUSTOMER_REPLY', DATEADD(HOUR, -5, @NowDt)),
        (2, 1, NULL, ISNULL(@EmpTechId, 1), N'Chào bạn, phòng Kỹ thuật đã tiếp nhận ticket TK-2026-0001 và đang kiểm tra service LibreOffice trên server.', 'STAFF_REPLY', DATEADD(HOUR, -4, @NowDt)),
        (3, 2, 2, NULL, N'Bên mình cần tài liệu hoặc video hướng dẫn gắn biến tự động vào phụ lục hợp đồng.', 'CUSTOMER_REPLY', DATEADD(HOUR, -3, @NowDt)),
        (4, 3, 1, NULL, N'Nhờ xuất hóa đơn GTGT theo mã số thuế 0101234567 giúp công ty mình nhé.', 'CUSTOMER_REPLY', DATEADD(DAY, -1, @NowDt)),
        (5, 3, NULL, ISNULL(@EmpAccId, 1), N'Phòng Kế toán đã xuất hóa đơn VAT và gửi đính kèm qua email của quý khách.', 'STAFF_REPLY', DATEADD(HOUR, -1, @NowDt));
    SET IDENTITY_INSERT [TICKET_MESSAGE] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TICKET_HISTORY] WHERE [ticket_support_id] = 1)
BEGIN
    SET IDENTITY_INSERT [TICKET_HISTORY] ON;
    INSERT INTO [TICKET_HISTORY] (
        [ticket_history_id], [ticket_support_id], [employee_id_start],
        [employee_id_finished], [changed_by_user_id], [notes],
        [old_status], [new_status], [changed_at]
    )
    VALUES
        (1, 1, NULL, CAST(ISNULL(@EmpTechId, 1) AS INT), CAST(ISNULL(@EmpMgrId, 1) AS INT), N'Phân công nhân viên kỹ thuật xử lý', 'OPEN', 'IN_PROGRESS', DATEADD(HOUR, -4, @NowDt)),
        (2, 3, CAST(ISNULL(@EmpAccId, 1) AS INT), CAST(ISNULL(@EmpAccId, 1) AS INT), CAST(ISNULL(@EmpAccId, 1) AS INT), N'Đã xuất hóa đơn và phản hồi khách hàng', 'IN_PROGRESS', 'RESOLVED', DATEADD(HOUR, -1, @NowDt));
    SET IDENTITY_INSERT [TICKET_HISTORY] OFF;
END;

COMMIT TRANSACTION;
GO

-- Kiểm tra danh sách tài khoản EMPLOYEE và TENANT sau khi seed
SELECT [employee_id], [employee_code], [employee_username], [employee_email], [employee_display_name], [employee_status] FROM [EMPLOYEE];
SELECT [tenant_id], [tenant_code], [tenant_name], [tenant_status] FROM [TENANT];
GO
