-- 016: KHACH HANG CO NGUOI PHU TRACH. Chay sau 005; chay lai nhieu lan khong sao.
-- Sale tao khach va chi thay, sua khach minh phu trach (CUSTOMER.customer_owner_employee_id).
-- Phong Hanh Chinh - Phap Che va Ban Giam Doc quan ly toan bo khach va chuyen nguoi phu trach (quyen customer.manage.all).
-- Khach cu: nguoi phu trach la nguoi da tao khach neu nguoi do la sale; con lai de trong, Hanh chinh gan sau.

SET NOCOUNT ON;

IF COL_LENGTH('CUSTOMER', 'customer_owner_employee_id') IS NULL
    ALTER TABLE CUSTOMER ADD customer_owner_employee_id BIGINT NULL
        CONSTRAINT FK_CUSTOMER_OWNER_EMPLOYEE REFERENCES EMPLOYEE (employee_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CUSTOMER_OWNER')
    CREATE INDEX IX_CUSTOMER_OWNER ON CUSTOMER (customer_owner_employee_id);
GO

DECLARE @now DATETIME2 = SYSUTCDATETIME();

INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, created_at)
SELECT g.system_permission_group_id, 'customer.manage.all', N'Quản lý toàn bộ khách hàng và chuyển người phụ trách', @now
FROM SYSTEM_PERMISSION_GROUP g
WHERE g.system_permission_group_code = 'INTERNAL_PAGES'
  AND NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION p WHERE p.system_permission_code = 'customer.manage.all');

UPDATE SYSTEM_PERMISSION SET system_permission_name = N'Tạo khách hàng và quản lý khách mình phụ trách'
WHERE system_permission_code = 'customer.manage';

INSERT INTO SYSTEM_ROLE_PERMISSION (system_role_id, system_permission_id, created_at)
SELECT r.system_role_id, p.system_permission_id, @now
FROM (VALUES ('INTERNAL_LEGAL'), ('INTERNAL_LEGAL_MANAGER'), ('INTERNAL_ADMIN')) v(role_code)
JOIN SYSTEM_ROLE r ON r.system_role_code = v.role_code
JOIN SYSTEM_PERMISSION p ON p.system_permission_code = 'customer.manage.all'
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE_PERMISSION x WHERE x.system_role_id = r.system_role_id AND x.system_permission_id = p.system_permission_id);

-- Khach cu chua co nguoi phu trach: giao cho nguoi tao neu nguoi do dang giu vai tro sale.
UPDATE c SET c.customer_owner_employee_id = c.created_by
FROM CUSTOMER c
WHERE c.customer_owner_employee_id IS NULL AND c.created_by IS NOT NULL
  AND EXISTS (SELECT 1 FROM EMPLOYEE_ROLE er JOIN SYSTEM_ROLE r ON r.system_role_id = er.system_role_id
              WHERE er.employee_id = c.created_by AND r.system_role_code IN ('INTERNAL_SALE', 'INTERNAL_SALE_MANAGER'));
