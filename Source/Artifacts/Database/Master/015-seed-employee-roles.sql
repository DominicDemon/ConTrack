-- 015: GAN VAI TRO CHO TAI KHOAN MAU. Chay sau seed-masterdb.sql va 005; chay lai nhieu lan khong sao.
-- seed-masterdb.sql chi xep nhan vien vao phong ban (EMPLOYEE_DEPARTMENT), khong gan EMPLOYEE_ROLE, nen tren database
-- dung moi khong ai co quyen nao, ke ca sysadmin, va khong ai vao duoc man "Nhan vien & phan quyen" de gan.
-- Script nay gan dung vai tro ma man do se gan: quan ly phong (sysadmin, test.manager, head.*) nhan
-- department_manager_role_id, nguoi con lai nhan department_staff_role_id. Chi them cho nhan vien CHUA co vai tro nao,
-- nen khong dung vao tai khoan da duoc phan quyen.

SET NOCOUNT ON;

DECLARE @managers TABLE (username VARCHAR(100) PRIMARY KEY);
INSERT INTO @managers VALUES ('sysadmin'), ('test.manager'), ('head.sale'), ('head.accountant'), ('head.legal');

INSERT INTO EMPLOYEE_ROLE (employee_id, system_role_id, created_by)
SELECT e.employee_id,
       CASE WHEN m.username IS NOT NULL THEN d.department_manager_role_id ELSE d.department_staff_role_id END,
       e.employee_id
FROM EMPLOYEE e
JOIN EMPLOYEE_DEPARTMENT ed ON ed.employee_id = e.employee_id
JOIN DEPARTMENT d ON d.department_id = ed.department_id
LEFT JOIN @managers m ON m.username = e.employee_username
WHERE NOT EXISTS (SELECT 1 FROM EMPLOYEE_ROLE r WHERE r.employee_id = e.employee_id)
  AND CASE WHEN m.username IS NOT NULL THEN d.department_manager_role_id ELSE d.department_staff_role_id END IS NOT NULL;

-- Moi phong mot quan ly (005): ghi nguoi quan ly vao phong neu phong chua co.
UPDATE d SET d.department_manager_employee_id = e.employee_id
FROM DEPARTMENT d
JOIN EMPLOYEE_DEPARTMENT ed ON ed.department_id = d.department_id
JOIN EMPLOYEE e ON e.employee_id = ed.employee_id
JOIN @managers m ON m.username = e.employee_username
WHERE d.department_manager_employee_id IS NULL;

SELECT e.employee_username, r.system_role_code
FROM EMPLOYEE_ROLE er JOIN EMPLOYEE e ON e.employee_id = er.employee_id JOIN SYSTEM_ROLE r ON r.system_role_id = er.system_role_id
ORDER BY e.employee_id;
