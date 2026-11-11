-- 003: bảng nội dung nháp mà MasterDB-Optimize_v1.sql bỏ đi nhưng code nháp / gửi khách xem vẫn dùng.
-- Định nghĩa giữ nguyên từ MasterDB-ConTrack.sql. Chỉ thêm, idempotent; chạy tay bằng SSMS hoặc sqlcmd -I (app không tự chạy).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_TEMPLATE_FIELDS')
BEGIN
    CREATE TABLE CONTRACT_TEMPLATE_FIELDS (
        contract_template_field_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_TEMPLATE_FIELDS PRIMARY KEY,
        contract_template_id BIGINT NOT NULL,
        contract_template_field_key VARCHAR(100) NOT NULL,
        contract_template_field_label NVARCHAR(255) NOT NULL,
        contract_template_field_data_type VARCHAR(50) NOT NULL DEFAULT 'TEXT',
        is_required BIT NOT NULL DEFAULT 1,
        extra_options NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_TemplateFields_TemplateId ON CONTRACT_TEMPLATE_FIELDS(contract_template_id);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DOCUMENT_ITEMS')
BEGIN
    CREATE TABLE CONTRACT_DOCUMENT_ITEMS (
        contract_document_item_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENT_ITEMS PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        product_id BIGINT NULL,
        contract_document_item_code VARCHAR(100) NULL,
        contract_document_item_name NVARCHAR(500) NOT NULL,
        contract_document_item_quantity DECIMAL(18,4) NOT NULL DEFAULT 1,
        contract_document_item_unit_price DECIMAL(18,2) NOT NULL,
        contract_document_item_line_subtotal DECIMAL(18,2) NOT NULL,
        contract_document_item_discount_percent DECIMAL(5,2) NOT NULL DEFAULT 0,
        contract_document_item_fixed_discount_amount DECIMAL(18,2) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_DocumentItems_DocumentId ON CONTRACT_DOCUMENT_ITEMS(contract_document_id);
    CREATE INDEX IX_DocumentItems_ProductId ON CONTRACT_DOCUMENT_ITEMS(product_id);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DYNAMIC_VALUES')
BEGIN
    CREATE TABLE CONTRACT_DYNAMIC_VALUES (
        contract_dynamic_value_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DYNAMIC_VALUES PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        field_key VARCHAR(100) NOT NULL,
        field_value NVARCHAR(MAX) NOT NULL
    );
    CREATE INDEX IX_Dynamic_Values_Key_Doc ON CONTRACT_DYNAMIC_VALUES(field_key, contract_document_id);
    CREATE INDEX IX_Dynamic_Values_DocumentId ON CONTRACT_DYNAMIC_VALUES(contract_document_id);
END;
GO
