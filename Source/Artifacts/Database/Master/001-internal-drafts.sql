SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Some company databases were provisioned from the older optimized script, which
-- did not create template fields. The internal draft bootstrap requires this table.
IF OBJECT_ID('dbo.CONTRACT_TEMPLATE_FIELDS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CONTRACT_TEMPLATE_FIELDS (
        contract_template_field_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_TEMPLATE_FIELDS PRIMARY KEY,
        contract_template_id BIGINT NOT NULL,
        contract_template_field_key VARCHAR(100) NOT NULL,
        contract_template_field_label NVARCHAR(255) NOT NULL,
        contract_template_field_data_type VARCHAR(50) NOT NULL CONSTRAINT DF_TemplateFields_DataType DEFAULT 'TEXT',
        is_required BIT NOT NULL CONSTRAINT DF_TemplateFields_IsRequired DEFAULT 1,
        extra_options NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_TemplateFields_TemplateId ON dbo.CONTRACT_TEMPLATE_FIELDS(contract_template_id);
END;
-- Older ConTrackDB deployments contain the document/version core tables but
-- omit these two tables required to save draft line items and form fields.
IF OBJECT_ID('dbo.CONTRACT_DOCUMENT_ITEMS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CONTRACT_DOCUMENT_ITEMS (
        contract_document_item_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENT_ITEMS PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        product_id BIGINT NULL,
        contract_document_item_code VARCHAR(100) NULL,
        contract_document_item_name NVARCHAR(500) NOT NULL,
        contract_document_item_quantity DECIMAL(18,4) NOT NULL CONSTRAINT DF_DocumentItems_Quantity DEFAULT 1,
        contract_document_item_unit_price DECIMAL(18,2) NOT NULL,
        contract_document_item_line_subtotal DECIMAL(18,2) NOT NULL,
        contract_document_item_discount_percent DECIMAL(5,2) NOT NULL CONSTRAINT DF_DocumentItems_DiscountPercent DEFAULT 0,
        contract_document_item_fixed_discount_amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_DocumentItems_FixedDiscount DEFAULT 0
    );
    CREATE INDEX IX_DocumentItems_DocumentId ON dbo.CONTRACT_DOCUMENT_ITEMS(contract_document_id);
    CREATE INDEX IX_DocumentItems_ProductId ON dbo.CONTRACT_DOCUMENT_ITEMS(product_id);
END;
IF OBJECT_ID('dbo.CONTRACT_DYNAMIC_VALUES', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CONTRACT_DYNAMIC_VALUES (
        contract_dynamic_value_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DYNAMIC_VALUES PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        field_key VARCHAR(100) NOT NULL,
        field_value NVARCHAR(MAX) NOT NULL
    );
    CREATE INDEX IX_Dynamic_Values_Key_Doc ON dbo.CONTRACT_DYNAMIC_VALUES(field_key, contract_document_id);
    CREATE INDEX IX_Dynamic_Values_DocumentId ON dbo.CONTRACT_DYNAMIC_VALUES(contract_document_id);
END;
IF COL_LENGTH('dbo.CONTRACT_DOCUMENTS', 'contract_document_current_version_id') = 4
    ALTER TABLE dbo.CONTRACT_DOCUMENTS ALTER COLUMN contract_document_current_version_id BIGINT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.EMPLOYEE') AND name = 'UX_Employee_Username')
    CREATE UNIQUE INDEX UX_Employee_Username ON dbo.EMPLOYEE(employee_username);
-- SQL Server unique indexes allow only one NULL unless explicitly filtered.
-- Quick customer creation deliberately does not require an email address.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CUSTOMER')
    AND name = 'UQ_Customer_Email_NotNull' AND has_filter = 0)
    DROP INDEX UQ_Customer_Email_NotNull ON dbo.CUSTOMER;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CUSTOMER') AND name = 'UQ_Customer_Email_NotNull')
    CREATE UNIQUE INDEX UQ_Customer_Email_NotNull ON dbo.CUSTOMER(customer_email) WHERE customer_email IS NOT NULL;
COMMIT;
