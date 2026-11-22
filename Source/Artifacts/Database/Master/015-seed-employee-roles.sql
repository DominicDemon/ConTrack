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

-- Migrate assignments to roles retired by 005 (notably INTERNAL_OFFICE).
-- Keep any already-active role; replace inactive assignments only when no active assignment exists.
;WITH RetiredAssignments AS (
    SELECT er.employee_id, MIN(er.employee_role_id) AS employee_role_id,
           CASE WHEN m.username IS NOT NULL THEN d.department_manager_role_id ELSE d.department_staff_role_id END AS target_role_id
    FROM EMPLOYEE_ROLE er
    JOIN SYSTEM_ROLE old_role ON old_role.system_role_id = er.system_role_id
    JOIN EMPLOYEE e ON e.employee_id = er.employee_id
    JOIN EMPLOYEE_DEPARTMENT ed ON ed.employee_id = e.employee_id
    JOIN DEPARTMENT d ON d.department_id = ed.department_id
    LEFT JOIN @managers m ON m.username = e.employee_username
    WHERE old_role.system_role_status <> 'ACTIVE'
      AND CASE WHEN m.username IS NOT NULL THEN d.department_manager_role_id ELSE d.department_staff_role_id END IS NOT NULL
    GROUP BY er.employee_id, CASE WHEN m.username IS NOT NULL THEN d.department_manager_role_id ELSE d.department_staff_role_id END
)
UPDATE er SET system_role_id = retired.target_role_id
FROM EMPLOYEE_ROLE er
JOIN RetiredAssignments retired ON retired.employee_role_id = er.employee_role_id
JOIN SYSTEM_ROLE old_role ON old_role.system_role_id = er.system_role_id AND old_role.system_role_status <> 'ACTIVE'
WHERE NOT EXISTS (
    SELECT 1 FROM EMPLOYEE_ROLE active_er
    JOIN SYSTEM_ROLE active_role ON active_role.system_role_id = active_er.system_role_id AND active_role.system_role_status = 'ACTIVE'
    WHERE active_er.employee_id = er.employee_id AND active_er.employee_role_id <> er.employee_role_id
);

-- Remove a retired assignment when the employee already has another active role.
DELETE er
FROM EMPLOYEE_ROLE er
JOIN SYSTEM_ROLE old_role ON old_role.system_role_id = er.system_role_id AND old_role.system_role_status <> 'ACTIVE'
WHERE EXISTS (
    SELECT 1 FROM EMPLOYEE_ROLE active_er
    JOIN SYSTEM_ROLE active_role ON active_role.system_role_id = active_er.system_role_id AND active_role.system_role_status = 'ACTIVE'
    WHERE active_er.employee_id = er.employee_id AND active_er.employee_role_id <> er.employee_role_id
);

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
