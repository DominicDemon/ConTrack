-- 005: phân quyền theo phòng ban. Mỗi nhân viên thuộc đúng 1 phòng; mỗi phòng có 1 quản lý.
-- Phòng ban quyết định màn hình: role nhân viên / role quản lý của phòng được gán vào EMPLOYEE_ROLE.
-- Idempotent; chạy tay bằng SSMS hoặc sqlcmd -I (app không tự chạy). Chạy sau 004.

-- 5 phòng ban, cùng mã và id với Artifacts/Sql/seed-masterdb.sql.
IF NOT EXISTS (SELECT 1 FROM DEPARTMENT WHERE department_code = 'BGD')
BEGIN
    SET IDENTITY_INSERT DEPARTMENT ON;
    INSERT INTO DEPARTMENT (department_id, department_code, department_name, department_description, department_status, created_at)
    VALUES (1, 'BGD',    N'Ban Giám Đốc',                N'Ban điều hành và phê duyệt cấp cao', 'ACTIVE', SYSUTCDATETIME()),
           (2, 'KTHUAT', N'Phòng Kỹ Thuật & Hỗ Trợ',     N'Phòng xử lý kỹ thuật và hỗ trợ khách hàng (Support Ticket)', 'ACTIVE', SYSUTCDATETIME()),
           (3, 'KD',     N'Phòng Kinh Doanh',            N'Phòng tư vấn và chăm sóc hợp đồng khách hàng', 'ACTIVE', SYSUTCDATETIME()),
           (4, 'KTTC',   N'Phòng Kế Toán - Tài Chính',   N'Phòng quản lý hóa đơn, thanh toán và công nợ', 'ACTIVE', SYSUTCDATETIME()),
           (5, 'HCNS',   N'Phòng Hành Chính - Pháp Chế', N'Phòng kiểm soát pháp lý và quản trị nội bộ', 'ACTIVE', SYSUTCDATETIME());
    SET IDENTITY_INSERT DEPARTMENT OFF;
END;
GO

-- Role của nhân viên và của quản lý trong phòng; người đang quản lý phòng (NULL = chưa có).
IF COL_LENGTH('dbo.DEPARTMENT', 'department_staff_role_id') IS NULL
    ALTER TABLE dbo.DEPARTMENT ADD department_staff_role_id BIGINT NULL;
IF COL_LENGTH('dbo.DEPARTMENT', 'department_manager_role_id') IS NULL
    ALTER TABLE dbo.DEPARTMENT ADD department_manager_role_id BIGINT NULL;
IF COL_LENGTH('dbo.DEPARTMENT', 'department_manager_employee_id') IS NULL
    ALTER TABLE dbo.DEPARTMENT ADD department_manager_employee_id BIGINT NULL;
GO
-- Một người quản lý tối đa một phòng; một nhân viên thuộc tối đa một phòng.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DEPARTMENT') AND name = 'UX_Department_Manager')
    CREATE UNIQUE INDEX UX_Department_Manager ON dbo.DEPARTMENT(department_manager_employee_id) WHERE department_manager_employee_id IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EMPLOYEE_DEPARTMENT') AND name = 'UX_EmployeeDepartment_Employee')
    CREATE UNIQUE INDEX UX_EmployeeDepartment_Employee ON dbo.EMPLOYEE_DEPARTMENT(employee_id);
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @now DATETIME2 = SYSUTCDATETIME();

-- Quyền theo màn hình. App kiểm tra theo mã; tên chỉ để hiển thị.
INSERT INTO SYSTEM_PERMISSION_GROUP (system_permission_group_code, system_permission_group_name, created_at)
SELECT v.code, v.name, @now
FROM (VALUES ('INTERNAL_DRAFTS', N'Hợp đồng nháp nội bộ'), ('INTERNAL_ACCOUNTS', N'Tài khoản nội bộ'), ('INTERNAL_PAGES', N'Màn hình nội bộ')) v(code, name)
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION_GROUP g WHERE g.system_permission_group_code = v.code);

INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, created_at)
SELECT g.system_permission_group_id, v.code, v.name, @now
FROM (VALUES
    ('INTERNAL_DRAFTS',   'draft.write',        N'Tạo và sửa hợp đồng nháp của mình'),
    ('INTERNAL_DRAFTS',   'draft.manage.all',   N'Quản lý hợp đồng nháp của cả phòng'),
    ('INTERNAL_ACCOUNTS', 'employee.manage',    N'Tạo tài khoản nhân viên, khóa / mở và phân quyền'),
    ('INTERNAL_PAGES',    'department.manage',  N'Quản lý phòng ban'),
    ('INTERNAL_PAGES',    'contract.approve',   N'Duyệt và ký hợp đồng'),
    ('INTERNAL_PAGES',    'audit.security',     N'Xem nhật ký bảo mật'),
    ('INTERNAL_PAGES',    'customer.manage',    N'Quản lý khách hàng'),
    ('INTERNAL_PAGES',    'product.read',       N'Xem sản phẩm và dịch vụ'),
    ('INTERNAL_PAGES',    'product.manage',     N'Quản lý sản phẩm và dịch vụ'),
    ('INTERNAL_PAGES',    'audit.contract',     N'Xem nhật ký hợp đồng'),
    ('INTERNAL_PAGES',    'ticket.handle',      N'Xử lý ticket hỗ trợ được giao'),
    ('INTERNAL_PAGES',    'ticket.manage.all',  N'Xem và phân công mọi ticket của phòng'),
    ('INTERNAL_PAGES',    'finance.manage',     N'Quản lý hóa đơn và thanh toán'),
    ('INTERNAL_PAGES',    'payment.approve',    N'Duyệt thanh toán và công nợ'),
    ('INTERNAL_PAGES',    'contract.read',      N'Xem hợp đồng (chỉ đọc)'),
    ('INTERNAL_PAGES',    'template.read',      N'Xem mẫu hợp đồng'),
    ('INTERNAL_PAGES',    'template.manage',    N'Quản lý mẫu hợp đồng'),
    ('INTERNAL_PAGES',    'legal.manage',       N'Quản lý hồ sơ pháp lý công ty')
) v(grp, code, name)
JOIN SYSTEM_PERMISSION_GROUP g ON g.system_permission_group_code = v.grp
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION p WHERE p.system_permission_code = v.code);

-- Role theo phòng. INTERNAL_SALE và INTERNAL_ADMIN giữ mã cũ.
DECLARE @roles TABLE (code VARCHAR(50), name NVARCHAR(200), dept VARCHAR(30), manager BIT);
INSERT INTO @roles VALUES
    ('INTERNAL_ADMIN',             N'Ban Giám Đốc (CEO)',            'BGD',    1),
    ('INTERNAL_SALE',              N'Nhân viên kinh doanh',          'KD',     0),
    ('INTERNAL_SALE_MANAGER',      N'Quản lý kinh doanh',            'KD',     1),
    ('INTERNAL_TECHNICAL',         N'Nhân viên kỹ thuật',            'KTHUAT', 0),
    ('INTERNAL_TECHNICAL_MANAGER', N'Quản lý kỹ thuật',              'KTHUAT', 1),
    ('INTERNAL_FINANCE',           N'Nhân viên kế toán',             'KTTC',   0),
    ('INTERNAL_FINANCE_MANAGER',   N'Quản lý kế toán - tài chính',   'KTTC',   1),
    ('INTERNAL_LEGAL',             N'Nhân viên hành chính - pháp chế', 'HCNS', 0),
    ('INTERNAL_LEGAL_MANAGER',     N'Quản lý hành chính - pháp chế', 'HCNS',   1);

INSERT INTO SYSTEM_ROLE (system_role_code, system_role_name, system_role_scope, system_role_status, created_at)
SELECT r.code, r.name, 'INTERNAL', 'ACTIVE', @now FROM @roles r
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE x WHERE x.system_role_code = r.code);
UPDATE x SET system_role_name = r.name, system_role_status = 'ACTIVE', updated_at = @now
FROM SYSTEM_ROLE x JOIN @roles r ON r.code = x.system_role_code WHERE x.system_role_name <> r.name;

-- Quyền của từng role (quản lý = quyền nhân viên + quyền quản lý). Đồng bộ đúng theo bảng này.
DECLARE @grants TABLE (role_code VARCHAR(50), perm VARCHAR(100));
INSERT INTO @grants VALUES
    ('INTERNAL_ADMIN', 'employee.manage'), ('INTERNAL_ADMIN', 'department.manage'), ('INTERNAL_ADMIN', 'contract.approve'), ('INTERNAL_ADMIN', 'audit.security'),
    ('INTERNAL_SALE', 'draft.write'), ('INTERNAL_SALE', 'customer.manage'), ('INTERNAL_SALE', 'product.read'),
    ('INTERNAL_SALE_MANAGER', 'draft.write'), ('INTERNAL_SALE_MANAGER', 'customer.manage'), ('INTERNAL_SALE_MANAGER', 'product.read'),
    ('INTERNAL_SALE_MANAGER', 'draft.manage.all'), ('INTERNAL_SALE_MANAGER', 'product.manage'), ('INTERNAL_SALE_MANAGER', 'audit.contract'),
    ('INTERNAL_TECHNICAL', 'ticket.handle'), ('INTERNAL_TECHNICAL', 'product.read'),
    ('INTERNAL_TECHNICAL_MANAGER', 'ticket.handle'), ('INTERNAL_TECHNICAL_MANAGER', 'product.read'), ('INTERNAL_TECHNICAL_MANAGER', 'ticket.manage.all'),
    ('INTERNAL_FINANCE', 'finance.manage'), ('INTERNAL_FINANCE', 'contract.read'),
    ('INTERNAL_FINANCE_MANAGER', 'finance.manage'), ('INTERNAL_FINANCE_MANAGER', 'contract.read'), ('INTERNAL_FINANCE_MANAGER', 'payment.approve'),
    ('INTERNAL_LEGAL', 'template.read'), ('INTERNAL_LEGAL', 'legal.manage'), ('INTERNAL_LEGAL', 'contract.read'),
    ('INTERNAL_LEGAL_MANAGER', 'template.read'), ('INTERNAL_LEGAL_MANAGER', 'legal.manage'), ('INTERNAL_LEGAL_MANAGER', 'contract.read'),
    ('INTERNAL_LEGAL_MANAGER', 'template.manage'),
    -- Hỗ trợ: ticket của main được gửi tới 4 phòng (Kỹ thuật, Kinh doanh, Kế toán, Pháp chế); trưởng phòng phân công trong phòng mình.
    ('INTERNAL_SALE', 'ticket.handle'), ('INTERNAL_SALE_MANAGER', 'ticket.handle'), ('INTERNAL_SALE_MANAGER', 'ticket.manage.all'),
    ('INTERNAL_FINANCE', 'ticket.handle'), ('INTERNAL_FINANCE_MANAGER', 'ticket.handle'), ('INTERNAL_FINANCE_MANAGER', 'ticket.manage.all'),
    ('INTERNAL_LEGAL', 'ticket.handle'), ('INTERNAL_LEGAL_MANAGER', 'ticket.handle'), ('INTERNAL_LEGAL_MANAGER', 'ticket.manage.all');

DELETE rp FROM SYSTEM_ROLE_PERMISSION rp
JOIN SYSTEM_ROLE r ON r.system_role_id = rp.system_role_id
JOIN SYSTEM_PERMISSION p ON p.system_permission_id = rp.system_permission_id
WHERE r.system_role_code IN (SELECT code FROM @roles)
  AND NOT EXISTS (SELECT 1 FROM @grants g WHERE g.role_code = r.system_role_code AND g.perm = p.system_permission_code);
INSERT INTO SYSTEM_ROLE_PERMISSION (system_role_id, system_permission_id, created_at)
SELECT r.system_role_id, p.system_permission_id, @now
FROM @grants g
JOIN SYSTEM_ROLE r ON r.system_role_code = g.role_code
JOIN SYSTEM_PERMISSION p ON p.system_permission_code = g.perm
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE_PERMISSION x WHERE x.system_role_id = r.system_role_id AND x.system_permission_id = p.system_permission_id);

-- Gắn role vào phòng. Ban Giám Đốc chỉ có quản lý (CEO), không có role nhân viên.
UPDATE d SET department_staff_role_id = s.system_role_id, department_manager_role_id = m.system_role_id, updated_at = @now
FROM DEPARTMENT d
JOIN @roles rm ON rm.dept = d.department_code AND rm.manager = 1
JOIN SYSTEM_ROLE m ON m.system_role_code = rm.code
LEFT JOIN @roles rs ON rs.dept = d.department_code AND rs.manager = 0
LEFT JOIN SYSTEM_ROLE s ON s.system_role_code = rs.code
WHERE ISNULL(d.department_staff_role_id, -1) <> ISNULL(s.system_role_id, -1) OR ISNULL(d.department_manager_role_id, -1) <> m.system_role_id;

-- INTERNAL_OFFICE (hành chính kiểm tra) được thay bằng Ban Giám Đốc: CEO duyệt và ký hợp đồng.
UPDATE s SET system_role_id = a.system_role_id
FROM APPROVAL_WORKFLOW_STEP s
JOIN SYSTEM_ROLE o ON o.system_role_id = s.system_role_id AND o.system_role_code = 'INTERNAL_OFFICE'
CROSS JOIN (SELECT system_role_id FROM SYSTEM_ROLE WHERE system_role_code = 'INTERNAL_ADMIN') a;
UPDATE SYSTEM_ROLE SET system_role_status = 'INACTIVE', updated_at = @now
WHERE system_role_code = 'INTERNAL_OFFICE' AND system_role_status <> 'INACTIVE';

COMMIT;
GO
