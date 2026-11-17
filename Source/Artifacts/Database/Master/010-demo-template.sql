-- [010] Mẫu hợp đồng minh họa INTERNAL_DEMO_V1 (database first, thay cho lệnh bootstrap --seed-internal cũ).
-- Nội dung lấy nguyên từ InternalBootstrap của nhánh feature-hoang: 18 trường + 3 điều khoản trống để sale tự nhập.
-- App đọc mẫu không có file lưu trữ trực tiếp từ CONTRACT_TEMPLATE + CONTRACT_TEMPLATE_FIELDS;
-- dòng có data type TERM là điều khoản (label = tiêu đề, extra_options = nội dung mặc định), theo thứ tự id.
-- Chạy sau 003-template-fields.sql (số 006–009 là script scan của nhánh main). Idempotent: chạy lại không tạo trùng. Chạy bằng sqlcmd cần -I.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM CONTRACT_TEMPLATE WHERE contract_template_code = 'INTERNAL_DEMO_V1')
    INSERT INTO CONTRACT_TEMPLATE (contract_template_code, contract_template_name, contract_template_description,
                                   contract_template_document_type, contract_template_language_mode, is_active, created_date)
    VALUES ('INTERNAL_DEMO_V1', N'Hợp đồng cung cấp sản phẩm/dịch vụ — mẫu minh họa',
            N'Khung từ Mẫu Contract Placeholder tiếng Việt; chưa được công ty phê duyệt.', 1, 1, 1, SYSUTCDATETIME());

DECLARE @template BIGINT = (SELECT contract_template_id FROM CONTRACT_TEMPLATE WHERE contract_template_code = 'INTERNAL_DEMO_V1');
DECLARE @rows TABLE (ord INT, field_key VARCHAR(100), label NVARCHAR(255), data_type VARCHAR(50), required BIT, content NVARCHAR(MAX));
INSERT INTO @rows VALUES
    ( 1, 'CUSTOMER_COMPANY',              N'Tên khách hàng / doanh nghiệp',     'TEXT', 1, NULL),
    ( 2, 'CUSTOMER_CONTACT_NAME',         N'Người đại diện khách hàng',         'TEXT', 1, NULL),
    ( 3, 'CUSTOMER_CONTACT_TITLE',        N'Chức vụ người đại diện khách',      'TEXT', 0, NULL),
    ( 4, 'CUSTOMER_CONTACT_PHONE',        N'SĐT khách hàng',                    'TEXT', 1, NULL),
    ( 5, 'CUSTOMER_EMAIL',                N'Email khách hàng',                  'TEXT', 0, NULL),
    ( 6, 'CUSTOMER_ADDRESS',              N'Địa chỉ khách hàng',                'TEXT', 1, NULL),
    ( 7, 'CUSTOMER_TAX_CODE',             N'Mã số thuế khách hàng',             'TEXT', 0, NULL),
    ( 8, 'CUSTOMER_BANK_ACCOUNT_NUMBER',  N'Tài khoản ngân hàng khách hàng',    'TEXT', 0, NULL),
    ( 9, 'CUSTOMER_BANK_NAME',            N'Ngân hàng khách hàng',              'TEXT', 0, NULL),
    (10, 'PROVIDER_LEGAL_NAME',           N'Tên pháp lý công ty mình',          'TEXT', 1, NULL),
    (11, 'PROVIDER_REPRESENTATIVE_NAME',  N'Người đại diện công ty',            'TEXT', 1, NULL),
    (12, 'PROVIDER_REPRESENTATIVE_TITLE', N'Chức vụ người đại diện công ty',    'TEXT', 0, NULL),
    (13, 'PROVIDER_ADDRESS',              N'Địa chỉ công ty',                   'TEXT', 1, NULL),
    (14, 'PROVIDER_PHONE',                N'SĐT công ty',                       'TEXT', 0, NULL),
    (15, 'PROVIDER_TAX_CODE',             N'Mã số thuế công ty',                'TEXT', 1, NULL),
    (16, 'PROVIDER_BANK_ACCOUNT_NUMBER',  N'Tài khoản ngân hàng công ty',       'TEXT', 0, NULL),
    (17, 'PROVIDER_BANK_NAME',            N'Ngân hàng công ty',                 'TEXT', 0, NULL),
    (18, 'CONTRACT_LEGAL_BASES',          N'Căn cứ hợp đồng',                   'TEXT', 0, NULL),
    (19, 'SCOPE',                         N'Phạm vi cung cấp',                  'TERM', 0, N''),
    (20, 'PAYMENT',                       N'Giá trị và điều kiện thanh toán',   'TERM', 0, N''),
    (21, 'DELIVERY',                      N'Bàn giao và nghiệm thu',            'TERM', 0, N'');

INSERT INTO CONTRACT_TEMPLATE_FIELDS (contract_template_id, contract_template_field_key, contract_template_field_label,
                                      contract_template_field_data_type, is_required, extra_options)
SELECT @template, r.field_key, r.label, r.data_type, r.required, r.content
FROM @rows r
WHERE NOT EXISTS (SELECT 1 FROM CONTRACT_TEMPLATE_FIELDS f WHERE f.contract_template_id = @template AND f.contract_template_field_key = r.field_key)
ORDER BY r.ord;

COMMIT;
GO
