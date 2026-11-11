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
