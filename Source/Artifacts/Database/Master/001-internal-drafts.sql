SET XACT_ABORT ON;
BEGIN TRANSACTION;
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
