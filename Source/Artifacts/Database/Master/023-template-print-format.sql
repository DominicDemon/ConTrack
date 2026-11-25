SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.CONTRACT_TEMPLATE', 'print_format_json') IS NULL
BEGIN
    ALTER TABLE dbo.CONTRACT_TEMPLATE ADD print_format_json nvarchar(2000) NULL;
    IF OBJECT_ID('dbo.CONTRACT_FORMAT_SETTING', 'U') IS NOT NULL
        EXEC(N'UPDATE t SET print_format_json = s.setting_value
               FROM dbo.CONTRACT_TEMPLATE t CROSS JOIN dbo.CONTRACT_FORMAT_SETTING s
               WHERE s.setting_id = 1 AND NOT EXISTS
               (SELECT 1 FROM dbo.FILE_STORAGE f WHERE f.file_storage_objectid = t.contract_template_id
                AND f.file_storage_objecttype = ''INTERNAL_TEMPLATE_WORD'')');
END;
COMMIT;
