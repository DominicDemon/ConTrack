-- 007: QUYỀN CHO BẢN KÝ GIẤY (F18, F19) VÀ HỢP ĐỒNG GIẤY SCAN VÀO HỆ THỐNG (SCANNED). CHỈ THÊM DỮ LIỆU, KHÔNG ĐỔI CẤU TRÚC BẢNG NÀO.
-- Hệ thống đăng nhập bằng EMPLOYEE (employee_username hoặc employee_email, employee_password_hash)
-- và lấy quyền từ EMPLOYEE_ROLE -> SYSTEM_ROLE_PERMISSION -> SYSTEM_PERMISSION, nên mỗi thao tác cần một mã quyền:
--   contract.signed_evidence.view    xem danh sách hợp đồng, mở bản ký, tải tệp, tìm theo từ khóa
--   contract.signed_evidence.upload  tải bản ký, thay bản ký (F18 bước 1-3, F19), sửa từ khóa
--   contract.signed_evidence.review  xác nhận hoặc từ chối bản ký (F18 bước 4-5); người có quyền này xem được mọi hợp đồng
--   contract.scanned.view            xem lượt scan và hợp đồng scan của mình
--   contract.scanned.upload          tải bản scan hợp đồng giấy cũ để OCR
--   contract.scanned.confirm         kiểm tra thông tin OCR và tạo hợp đồng SCANNED; xem được mọi lượt scan
--   system.encryption_key.manage     cấp key 12 ký tự cho nhân viên để mở tệp đã mã hóa
-- MasterDB-Optimize.sql chưa có dòng nào trong SYSTEM_PERMISSION, nên nhóm cần chốt mã quyền và gán cho vai trò thật
-- (ví dụ người tải: view + upload; người kiểm tra: view + review). Script này chỉ tạo quyền, KHÔNG gán cho vai trò nào.
-- Chạy lại nhiều lần không tạo dòng trùng.

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'CONTRACT_SIGNED_EVIDENCE')
    INSERT INTO SYSTEM_PERMISSION_GROUP (system_permission_group_code, system_permission_group_name, system_permission_group_description)
    VALUES ('CONTRACT_SIGNED_EVIDENCE', N'Bản ký giấy của hợp đồng', N'Tải, thay, kiểm tra và tra cứu bản scan hợp đồng đã ký giấy');

DECLARE @groupId BIGINT = (SELECT system_permission_group_id FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'CONTRACT_SIGNED_EVIDENCE');

DECLARE @permissions TABLE (code VARCHAR(100), name NVARCHAR(300), action VARCHAR(50), description NVARCHAR(2000));
INSERT INTO @permissions VALUES
    ('contract.signed_evidence.view',   N'Xem bản ký giấy',            'VIEW',   N'Xem danh sách hợp đồng, mở bản ký, tải tệp, tìm hợp đồng theo từ khóa OCR'),
    ('contract.signed_evidence.upload', N'Tải và thay bản ký giấy',    'UPLOAD', N'Tải bản scan hợp đồng đã ký (F18), thay bản scan (F19), thêm hoặc xóa từ khóa'),
    ('contract.signed_evidence.review', N'Kiểm tra bản ký giấy',       'REVIEW', N'Xác nhận hợp lệ hoặc từ chối bản ký sau khi xem chữ ký và con dấu (F18); xem được mọi hợp đồng');

INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, system_permission_resource, system_permission_action, system_permission_description)
SELECT @groupId, p.code, p.name, 'contract.signed_evidence', p.action, p.description
FROM @permissions p
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION x WHERE x.system_permission_code = p.code);

-- Hợp đồng giấy cũ đưa vào hệ thống bằng cách scan (CONTRACTS.source_type = SCANNED).
IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'CONTRACT_SCANNED')
    INSERT INTO SYSTEM_PERMISSION_GROUP (system_permission_group_code, system_permission_group_name, system_permission_group_description)
    VALUES ('CONTRACT_SCANNED', N'Hợp đồng giấy scan vào hệ thống', N'Scan hợp đồng giấy cũ, OCR, kiểm tra và tạo hợp đồng SCANNED');
DECLARE @scannedGroupId BIGINT = (SELECT system_permission_group_id FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'CONTRACT_SCANNED');
DECLARE @scanned TABLE (code VARCHAR(100), name NVARCHAR(300), action VARCHAR(50), description NVARCHAR(2000));
INSERT INTO @scanned VALUES
    ('contract.scanned.view',    N'Xem hợp đồng scan',              'VIEW',    N'Xem lượt scan của mình, hợp đồng scan mình tạo và tệp của chúng'),
    ('contract.scanned.upload',  N'Tải bản scan hợp đồng giấy',      'UPLOAD',  N'Tải một hoặc nhiều bản scan hợp đồng giấy cũ để OCR, đọc lại khi lỗi'),
    ('contract.scanned.confirm', N'Kiểm tra và tạo hợp đồng scan',   'CONFIRM', N'Kiểm tra thông tin OCR đọc được và tạo hợp đồng SCANNED; xem được mọi lượt scan');
INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, system_permission_resource, system_permission_action, system_permission_description)
SELECT @scannedGroupId, p.code, p.name, 'contract.scanned', p.action, p.description
FROM @scanned p
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION x WHERE x.system_permission_code = p.code);

-- Cấp key 12 ký tự để mở tệp đã mã hóa (008-file-encryption.sql).
IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'FILE_ENCRYPTION')
    INSERT INTO SYSTEM_PERMISSION_GROUP (system_permission_group_code, system_permission_group_name, system_permission_group_description)
    VALUES ('FILE_ENCRYPTION', N'Key mở tệp mã hóa', N'Cấp key 12 ký tự cho nhân viên để mở bản scan đã mã hóa');
IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION WHERE system_permission_code = 'system.encryption_key.manage')
    INSERT INTO SYSTEM_PERMISSION (system_permission_group_id, system_permission_code, system_permission_name, system_permission_resource, system_permission_action, system_permission_description)
    SELECT system_permission_group_id, 'system.encryption_key.manage', N'Cấp key mở tệp', 'system.encryption_key', 'MANAGE',
           N'Cấp hoặc cấp lại key 12 ký tự cho nhân viên; key cũ thôi dùng được'
    FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'FILE_ENCRYPTION';

-- Gán quyền scan/ký giấy cho Phòng Hành Chính - Pháp Chế (INTERNAL_LEGAL, INTERNAL_LEGAL_MANAGER) và quyền cấp key cho quản lý/BGD.
DECLARE @roleGrants TABLE (role_code VARCHAR(50), perm_code VARCHAR(100));
INSERT INTO @roleGrants VALUES
    ('INTERNAL_LEGAL', 'contract.signed_evidence.view'),
    ('INTERNAL_LEGAL', 'contract.signed_evidence.upload'),
    ('INTERNAL_LEGAL', 'contract.signed_evidence.review'),
    ('INTERNAL_LEGAL', 'contract.scanned.view'),
    ('INTERNAL_LEGAL', 'contract.scanned.upload'),
    ('INTERNAL_LEGAL', 'contract.scanned.confirm'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.signed_evidence.view'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.signed_evidence.upload'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.signed_evidence.review'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.scanned.view'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.scanned.upload'),
    ('INTERNAL_LEGAL_MANAGER', 'contract.scanned.confirm'),
    ('INTERNAL_LEGAL_MANAGER', 'system.encryption_key.manage'),
    ('INTERNAL_ADMIN', 'system.encryption_key.manage');

INSERT INTO SYSTEM_ROLE_PERMISSION (system_role_id, system_permission_id, created_at)
SELECT r.system_role_id, p.system_permission_id, SYSUTCDATETIME()
FROM @roleGrants g
JOIN SYSTEM_ROLE r ON r.system_role_code = g.role_code
JOIN SYSTEM_PERMISSION p ON p.system_permission_code = g.perm_code
WHERE NOT EXISTS (
    SELECT 1 FROM SYSTEM_ROLE_PERMISSION rp
    WHERE rp.system_role_id = r.system_role_id AND rp.system_permission_id = p.system_permission_id
);

COMMIT TRANSACTION;
