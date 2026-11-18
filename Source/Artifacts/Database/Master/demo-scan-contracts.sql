-- DU LIEU MAU DE THU LUONG SCAN (chi chay tren database thu nghiem, KHONG chay tren database that).
-- Tao 4 khach hang va 4 hop dong co ma co dinh de thu luong A (ban ky giay):
--   1001, 1002, 1003: dang Cho ky (PendingSignature) -> tai duoc ban ky giay.
--   1004: da ky online bang OTP -> khong nhan ban ky giay.
-- Chay lai nhieu lan duoc: moi lan dat lai trang thai 4 hop dong ve nhu ban dau (de demo lai).
-- Chay sau 001-012. Neu da chay 010-demo-template.sql thi dung mau INTERNAL_DEMO_V1, neu chua thi tao mau MAU-HD-DEMO.

SET NOCOUNT ON; SET XACT_ABORT ON;

-- An toan: cac ma 501-504 / 1001-1004 da duoc dung cho du lieu khac thi dung lai, khong ghi de.
IF EXISTS (SELECT 1 FROM CONTRACT_DOCUMENTS
           WHERE contract_document_id BETWEEN 1001 AND 1004
             AND contract_document_code NOT IN ('HD-20261026-000001', 'HD-20261020-000002', 'HD-20261015-000003', 'HD-20261101-000004'))
   OR EXISTS (SELECT 1 FROM CUSTOMER WHERE customer_id BETWEEN 501 AND 504 AND customer_email NOT LIKE 'khach50[1-4]@example.com')
BEGIN
    RAISERROR(N'Database nay da co du lieu khac o cac ma 501-504 / 1001-1004. Day khong phai database thu nghiem: dung lai.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- Nguoi phu trach: nhan vien hanh chinh dau tien (INTERNAL_LEGAL), neu khong co thi nhan vien dau tien.
DECLARE @emp BIGINT = (
    SELECT TOP 1 e.employee_id FROM EMPLOYEE e
    JOIN EMPLOYEE_ROLE er ON er.employee_id = e.employee_id
    JOIN SYSTEM_ROLE r ON r.system_role_id = er.system_role_id
    WHERE r.system_role_code = 'INTERNAL_LEGAL' ORDER BY e.employee_id);
IF @emp IS NULL SET @emp = (SELECT TOP 1 employee_id FROM EMPLOYEE ORDER BY employee_id);

DECLARE @template BIGINT = (SELECT contract_template_id FROM CONTRACT_TEMPLATE WHERE contract_template_code = 'INTERNAL_DEMO_V1');
IF @template IS NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM CONTRACT_TEMPLATE WHERE contract_template_code = 'MAU-HD-DEMO')
        INSERT INTO CONTRACT_TEMPLATE (contract_template_code, contract_template_name, contract_template_document_type)
        VALUES ('MAU-HD-DEMO', N'Mẫu hợp đồng (demo scan)', 1);
    SET @template = (SELECT contract_template_id FROM CONTRACT_TEMPLATE WHERE contract_template_code = 'MAU-HD-DEMO');
END

DECLARE @customers TABLE (id BIGINT, business_id BIGINT, name NVARCHAR(400), tax VARCHAR(20), phone VARCHAR(20));
INSERT INTO @customers VALUES
    (501, 501, N'Công ty TNHH Thương mại ABC', '0312345678', '0901234567'),
    (502, 502, N'Công ty CP Dịch vụ DEF', '0107654321', '0987654321'),
    (503, 503, N'Công ty TNHH Sản xuất GHI', '3600123456', '02838221234'),
    (504, 504, N'Công ty TNHH Công nghệ KLM', '0315556677', '0933445566');

SET IDENTITY_INSERT CUSTOMER ON;
INSERT INTO CUSTOMER (customer_id, customer_email, customer_personal_phone, customer_password_hash, customer_display_name)
SELECT c.id, CONCAT('khach', c.id, '@example.com'), c.phone, '!demo-no-login', c.name FROM @customers c
WHERE NOT EXISTS (SELECT 1 FROM CUSTOMER x WHERE x.customer_id = c.id);
SET IDENTITY_INSERT CUSTOMER OFF;

SET IDENTITY_INSERT CUSTOMER_BUSINESS ON;
INSERT INTO CUSTOMER_BUSINESS (customer_business_id, customer_id, customer_business_legal_name, customer_business_company_tax_identifier)
SELECT c.business_id, c.id, c.name, c.tax FROM @customers c
WHERE NOT EXISTS (SELECT 1 FROM CUSTOMER_BUSINESS x WHERE x.customer_id = c.id);
SET IDENTITY_INSERT CUSTOMER_BUSINESS OFF;

DECLARE @contracts TABLE (
    document_id BIGINT, version_id BIGINT, version_no BIGINT, customer_id BIGINT, code VARCHAR(50), name NVARCHAR(1000),
    status INT, subtotal DECIMAL(18,2), vat DECIMAL(18,2), total DECIMAL(18,2), sign_date DATETIME2);
INSERT INTO @contracts VALUES
    (1001, 2101, 1, 501, 'HD-20261026-000001', N'Hợp đồng cung cấp phần mềm', 3, 1363636364, 136363636, 1500000000, NULL),
    (1002, 2102, 1, 502, 'HD-20261020-000002', N'Hợp đồng dịch vụ bảo trì phần mềm', 3, 218181818, 21818182, 240000000, NULL),
    (1003, 2103, 1, 503, 'HD-20261015-000003', N'Hợp đồng triển khai hệ thống', 3, 781818182, 78181818, 860000000, NULL),
    (1004, 2104, 1, 504, 'HD-20261101-000004', N'Hợp đồng thuê dịch vụ lưu trữ', 4, 87272727, 8727273, 96000000, '2026-11-02T03:00:00');

SET IDENTITY_INSERT CONTRACT_DOCUMENTS ON;
INSERT INTO CONTRACT_DOCUMENTS (contract_document_id, contract_document_current_version_id, customer_id, employee_id,
    contract_template_id, contract_document_code, contract_document_name, contract_document_type, contract_document_status)
SELECT c.document_id, c.version_id, c.customer_id, @emp, @template, c.code, c.name, 1, c.status FROM @contracts c
WHERE NOT EXISTS (SELECT 1 FROM CONTRACT_DOCUMENTS x WHERE x.contract_document_id = c.document_id);
SET IDENTITY_INSERT CONTRACT_DOCUMENTS OFF;

SET IDENTITY_INSERT CONTRACT_VERSION ON;
INSERT INTO CONTRACT_VERSION (contract_version_id, contract_document_id, contract_version_version_no, contract_template_id,
    contract_version_subtotal, contract_version_totalvat, contract_version_totalamount,
    contract_version_islocked, contract_version_lockeddate, contract_version_lockedbyemployeeid, contract_version_signdate)
SELECT c.version_id, c.document_id, c.version_no, @template, c.subtotal, c.vat, c.total, 1, '2026-10-28T03:00:00', @emp, c.sign_date
FROM @contracts c
WHERE NOT EXISTS (SELECT 1 FROM CONTRACT_VERSION x WHERE x.contract_document_id = c.document_id);
SET IDENTITY_INSERT CONTRACT_VERSION OFF;

-- Demo lan truoc co the da chuyen hop dong sang Da ky: dat lai trang thai ban dau.
UPDATE d SET d.contract_document_status = c.status
FROM CONTRACT_DOCUMENTS d JOIN @contracts c ON c.document_id = d.contract_document_id;
UPDATE v SET v.contract_version_signdate = c.sign_date, v.contract_version_islocked = 1
FROM CONTRACT_VERSION v JOIN @contracts c ON c.document_id = v.contract_document_id;

-- Chu ky online (OTP) chi co o hop dong 1004: ca hai ben da ky.
DELETE FROM CONTRACT_ELECTRONIC_SIGNATURE WHERE contract_document_id BETWEEN 1001 AND 1004;
INSERT INTO CONTRACT_ELECTRONIC_SIGNATURE (contract_document_id, contract_version_id, contract_electronic_partytype,
    contract_electronic_signertype, employee_id, contract_electronic_signername, contract_electronic_signerphone,
    contract_electronic_signaturetype, contract_electronic_status, contract_electronic_signedat)
SELECT d.contract_document_id, d.contract_document_current_version_id, p.partytype, p.signertype, p.employee_id, p.signername, p.phone, 'OTP_ACK', 1, '2026-11-02T03:00:00'
FROM CONTRACT_DOCUMENTS d
CROSS APPLY (VALUES
    (1, 'ExternalCustomer', CAST(NULL AS BIGINT), N'Đại diện Công ty TNHH Công nghệ KLM', '0933445566'),
    (2, 'InternalEmployee', @emp, N'Đại diện công ty', '0243999888')) p(partytype, signertype, employee_id, signername, phone)
WHERE d.contract_document_id = 1004;

COMMIT;
SELECT d.contract_document_id, d.contract_document_code, d.contract_document_status
FROM CONTRACT_DOCUMENTS d WHERE d.contract_document_id BETWEEN 1001 AND 1004;
