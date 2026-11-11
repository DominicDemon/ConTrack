-- 004: admin quản lý tài khoản nhân viên và phân quyền.
-- Chỉ thêm / nới ràng buộc, idempotent; chạy tay bằng SSMS hoặc sqlcmd -I (app không tự chạy).

-- Email / SĐT / số định danh: UNIQUE thường chỉ cho một NULL -> đổi sang unique có lọc.
-- Họ tên hiển thị: bỏ unique, hai nhân viên được phép trùng tên (đã có mã và username để phân biệt).
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE dbo.EMPLOYEE DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';'
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.EMPLOYEE') AND kc.type = 'UQ'
  AND c.name IN ('employee_email', 'employee_phone', 'employee_personal_identifier', 'employee_display_name');
EXEC sp_executesql @sql;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EMPLOYEE') AND name = 'UX_Employee_Email')
    CREATE UNIQUE INDEX UX_Employee_Email ON dbo.EMPLOYEE(employee_email) WHERE employee_email IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EMPLOYEE') AND name = 'UX_Employee_Phone')
    CREATE UNIQUE INDEX UX_Employee_Phone ON dbo.EMPLOYEE(employee_phone) WHERE employee_phone IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EMPLOYEE') AND name = 'UX_Employee_PersonalIdentifier')
    CREATE UNIQUE INDEX UX_Employee_PersonalIdentifier ON dbo.EMPLOYEE(employee_personal_identifier) WHERE employee_personal_identifier IS NOT NULL;
GO

-- 1 = admin vừa tạo tài khoản / đặt lại mật khẩu tạm; nhân viên phải đổi mật khẩu trước khi dùng hệ thống.
IF COL_LENGTH('dbo.EMPLOYEE', 'employee_must_change_password') IS NULL
    ALTER TABLE dbo.EMPLOYEE ADD employee_must_change_password BIT NOT NULL
        CONSTRAINT DF_Employee_MustChangePassword DEFAULT 0;
GO

-- Quyền quản lý tài khoản và vai trò quản trị nội bộ (app kiểm tra mã employee.manage).
IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'INTERNAL_ACCOUNTS')
    INSERT INTO SYSTEM_PERMISSION_GROUP (system_permission_group_code, system_permission_group_name, created_at)
    VALUES ('INTERNAL_ACCOUNTS', N'Tài khoản nội bộ', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION WHERE system_permission_code = 'employee.manage')
    INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, created_at)
    SELECT system_permission_group_id, 'employee.manage', N'Tạo tài khoản nhân viên, khóa / mở và phân quyền', SYSUTCDATETIME()
    FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'INTERNAL_ACCOUNTS';

IF NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE WHERE system_role_code = 'INTERNAL_ADMIN')
    INSERT INTO SYSTEM_ROLE (system_role_code, system_role_name, system_role_scope, system_role_status, created_at)
    VALUES ('INTERNAL_ADMIN', N'Quản trị tài khoản', 'INTERNAL', 'ACTIVE', SYSUTCDATETIME());

INSERT INTO SYSTEM_ROLE_PERMISSION (system_role_id, system_permission_id, created_at)
SELECT r.system_role_id, p.system_permission_id, SYSUTCDATETIME()
FROM SYSTEM_ROLE r JOIN SYSTEM_PERMISSION p ON p.system_permission_code = 'employee.manage'
WHERE r.system_role_code = 'INTERNAL_ADMIN'
  AND NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE_PERMISSION x WHERE x.system_role_id = r.system_role_id AND x.system_permission_id = p.system_permission_id);
GO
