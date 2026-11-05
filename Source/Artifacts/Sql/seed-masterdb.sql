/* =====================================================================================
   SEED DATA CHO DATABASE: MasterDB (MasterDB-Optimize_v1.sql)
   - Có thể chạy lại nhiều lần (Idempotent: kiểm tra IF NOT EXISTS trước khi INSERT)
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
IF NOT EXISTS (SELECT 1 FROM [CATEGORIES] WHERE [category_name] = N'Phần mềm quản lý doanh nghiệp')
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
-- 4. EMPLOYEE (Nhân viên & System Admin - Mật khẩu: 123456aA45$%)
-- =====================================================================================
IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE] WHERE [employee_username] = 'sysadmin')
BEGIN
    SET IDENTITY_INSERT [EMPLOYEE] ON;
    INSERT INTO [EMPLOYEE] (
        [employee_id], [employee_code], [employee_email], [employee_phone],
        [employee_password_hash], [employee_display_name], [employee_username],
        [employee_personal_identifier], [employee_status], [employee_dob],
        [employee_gender], [employee_address], [employee_email_verified_at],
        [employee_last_login_at], [created_at]
    )
    VALUES
        (1, 'ADM-SYSADMIN', 'Me@gmail.com',             '0901000001', @DefaultPasswordHash, N'BossVipPro (System Admin)',    'sysadmin',        '001099000001', 'ACTIVE', '1990-01-01', 'MALE',   N'Hà Nội', @Now, @Now, @Now),
        (2, 'EMP-MANAGER',  'manager@contrack.vn',      '0901000002', @DefaultPasswordHash, N'Nguyễn Văn Quản Lý',           'test.manager',    '001099000002', 'ACTIVE', '1992-05-10', 'MALE',   N'Hà Nội', @Now, @Now, @Now),
        (3, 'EMP-TECH01',   'hungnguyennn119@gmail.com','0901000003', @DefaultPasswordHash, N'Trần Kỹ Thuật (Support Lead)', 'test.technical',  '001099000003', 'ACTIVE', '1996-08-15', 'MALE',   N'Hà Nội', @Now, @Now, @Now),
        (4, 'EMP-SALE01',   'sale@contrack.vn',         '0901000004', @DefaultPasswordHash, N'Lê Thị Kinh Doanh',            'test.sale',       '001099000004', 'ACTIVE', '1997-03-20', 'FEMALE', N'TP.HCM', @Now, @Now, @Now),
        (5, 'EMP-ACC01',    'accountant@contrack.vn',   '0901000005', @DefaultPasswordHash, N'Phạm Kế Toán',                 'test.accountant', '001099000005', 'ACTIVE', '1995-11-12', 'FEMALE', N'Hà Nội', @Now, @Now, @Now);
    SET IDENTITY_INSERT [EMPLOYEE] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [EMPLOYEE_DEPARTMENT] WHERE [employee_id] = 1)
BEGIN
    INSERT INTO [EMPLOYEE_DEPARTMENT] ([employee_id], [department_id], [created_at], [created_by])
    VALUES
        (1, 1, @Now, 1),
        (2, 1, @Now, 1),
        (3, 2, @Now, 1),
        (4, 3, @Now, 1),
        (5, 4, @Now, 1);
END;

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
        (1, 'contact@abc-corp.vn',   '0912345001', @DefaultPasswordHash, N'Công ty Cổ phần Công nghệ ABC',     '0101234567', 1, '1988-04-12', 'MALE',   N'Tầng 12, Tòa nhà Keangnam, Nam Từ Liêm, Hà Nội', @Now, 1),
        (2, 'info@minhphat-group.vn','0912345002', @DefaultPasswordHash, N'Công ty TNHH Thương mại Minh Phát', '0309876543', 1, '1991-09-25', 'FEMALE', N'Số 45 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh',     @Now, 1),
        (3, 'support@vietstar.vn',   '0912345003', @DefaultPasswordHash, N'Tập đoàn Đầu tư Sao Việt',          '0105556668', 1, '1985-12-01', 'MALE',   N'Số 88 Láng Hạ, Đống Đa, Hà Nội',                @Now, 1);
    SET IDENTITY_INSERT [CUSTOMER] OFF;
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
        (1, 1, N'CÔNG TY CỔ PHẦN CÔNG NGHỆ ABC',     '0101234567', '0243888999', 'contact@abc-corp.vn',    N'Nam Từ Liêm, Hà Nội', N'Nam Từ Liêm, Hà Nội', 'JSC', N'Nguyễn Thành Nam', @Now, 1),
        (2, 2, N'CÔNG TY TNHH THƯƠNG MẠI MINH PHÁT', '0309876543', '0283777888', 'info@minhphat-group.vn', N'Quận 1, TP.HCM',      N'Quận 1, TP.HCM',      'LLC', N'Trần Minh Phát',   @Now, 1);
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
    SET IDENTITY_INSERT [TENANT] ON;
    INSERT INTO [TENANT] (
        [tenant_id], [customer_id], [saas_type_id], [package_level_id],
        [tenant_code], [tenant_name], [tenant_subdomain], [tenant_status],
        [tenant_expired_at], [activated_at], [created_at]
    )
    VALUES
        (1, 1, 1, 2, 'dtc',     N'DTC Technology (Default Tenant)', 'dtc',     'ACTIVE', DATEADD(YEAR, 1, @Now), @NowDt, @Now),
        (2, 2, 1, 2, 'minhphat',N'Công ty TNHH Minh Phát',          'minhphat','ACTIVE', DATEADD(YEAR, 1, @Now), @NowDt, @Now);
    SET IDENTITY_INSERT [TENANT] OFF;
END;

IF NOT EXISTS (SELECT 1 FROM [TENANT_DATABASE] WHERE [tenant_id] = 1)
BEGIN
    SET IDENTITY_INSERT [TENANT_DATABASE] ON;
    INSERT INTO [TENANT_DATABASE] (
        [tenant_database_id], [tenant_id], [tenant_database_provider],
        [tenant_database_host], [tenant_database_port], [tenant_database_db_name],
        [tenant_database_user], [tenant_database_provisioning_status], [created_at]
    )
    VALUES
        (1, 1, 'SqlServer', 'localhost', 1433, 'MasterDB', 'sa', 'READY', @Now),
        (2, 2, 'SqlServer', 'localhost', 1433, 'MasterDB', 'sa', 'READY', @Now);
    SET IDENTITY_INSERT [TENANT_DATABASE] OFF;
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
        (1, 1, 2, 1, 1, 2, 'TK-2026-0001',
         N'Không xuất được file PDF hợp đồng song ngữ',
         N'Khi nhấn xem trước PDF ở hợp đồng mẫu song ngữ Việt - Anh thì hệ thống báo lỗi timeout.',
         'HIGH', 'IN_PROGRESS',
         NULL, NULL, NULL,
         DATEADD(HOUR, -5, @NowDt), DATEADD(HOUR, -2, @NowDt)),

        (2, 2, 3, 2, 1, 1, 'TK-2026-0002',
         N'Hỗ trợ cấu hình trường dữ liệu động (Placeholder) cho phụ lục',
         N'Nhờ bộ phận hỗ trợ hướng dẫn cách thêm bảng danh mục hàng hóa tự động vào mẫu phụ lục hợp đồng.',
         'MEDIUM', 'OPEN',
         NULL, NULL, NULL,
         DATEADD(HOUR, -3, @NowDt), DATEADD(HOUR, -3, @NowDt)),

        (3, 1, 4, 1, 2, NULL, 'TK-2026-0003',
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
        (1, 1, 3, 'PRIMARY', DATEADD(HOUR, -4, @NowDt), NULL, 2),
        (2, 3, 5, 'PRIMARY', DATEADD(HOUR, -12, @NowDt), NULL, 2);
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
        (2, 1, NULL, 3, N'Chào bạn, phòng Kỹ thuật đã tiếp nhận ticket TK-2026-0001 và đang kiểm tra service LibreOffice trên server.', 'STAFF_REPLY', DATEADD(HOUR, -4, @NowDt)),
        (3, 2, 2, NULL, N'Bên mình cần tài liệu hoặc video hướng dẫn gắn biến tự động vào phụ lục hợp đồng.', 'CUSTOMER_REPLY', DATEADD(HOUR, -3, @NowDt)),
        (4, 3, 1, NULL, N'Nhờ xuất hóa đơn GTGT theo mã số thuế 0101234567 giúp công ty mình nhé.', 'CUSTOMER_REPLY', DATEADD(DAY, -1, @NowDt)),
        (5, 3, NULL, 5, N'Phòng Kế toán đã xuất hóa đơn VAT và gửi đính kèm qua email của quý khách.', 'STAFF_REPLY', DATEADD(HOUR, -1, @NowDt));
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
        (1, 1, NULL, 3, 2, N'Phân công nhân viên kỹ thuật xử lý', 'OPEN', 'IN_PROGRESS', DATEADD(HOUR, -4, @NowDt)),
        (2, 3, 5,    5, 5, N'Đã xuất hóa đơn và phản hồi khách hàng', 'IN_PROGRESS', 'RESOLVED', DATEADD(HOUR, -1, @NowDt));
    SET IDENTITY_INSERT [TICKET_HISTORY] OFF;
END;

COMMIT TRANSACTION;
GO

-- Kiểm tra nhanh kết quả sau khi seed
SELECT 'DEPARTMENT' AS TableName, COUNT(*) AS TotalRows FROM [DEPARTMENT]
UNION ALL SELECT 'EMPLOYEE', COUNT(*) FROM [EMPLOYEE]
UNION ALL SELECT 'CUSTOMER', COUNT(*) FROM [CUSTOMER]
UNION ALL SELECT 'TENANT', COUNT(*) FROM [TENANT]
UNION ALL SELECT 'PRODUCTS', COUNT(*) FROM [PRODUCTS]
UNION ALL SELECT 'SERVICES', COUNT(*) FROM [SERVICES]
UNION ALL SELECT 'TICKET_TYPES', COUNT(*) FROM [TICKET_TYPES]
UNION ALL SELECT 'TICKET_SUPPORT', COUNT(*) FROM [TICKET_SUPPORT]
UNION ALL SELECT 'TICKET_MESSAGE', COUNT(*) FROM [TICKET_MESSAGE];
GO

