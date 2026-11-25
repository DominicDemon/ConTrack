-- 022: KHO LUU TRU CUA KHACH HANG. Chay sau 017; chay lai nhieu lan khong sao.
-- Khach dung dich vu cua cong ty de tu scan va luu hop dong, tai lieu cua minh (ke ca hop dong voi ben khac).
-- Tep duoc ma hoa cho key cua khach (CUSTOMER_FILE_KEY, 017) va ban du phong cua cong ty; KHONG cap cho nhan vien
-- nao: nhan vien khong xem duoc kho nay. Tu khoa OCR luu rieng (CUSTOMER_VAULT_KEYWORD), khong chung voi
-- CONTRACT_SCAN_KEYWORD, nen man tim kiem cua nhan vien khong thay. Moi khach toi da 1 GB (kiem tra trong ung dung).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_VAULT_FILE')
BEGIN
    CREATE TABLE CUSTOMER_VAULT_FILE (
        customer_vault_file_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CUSTOMER_VAULT_FILE PRIMARY KEY,
        customer_id BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_VAULT_FILE_CUSTOMER REFERENCES CUSTOMER (customer_id),
        file_storage_fileid BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_VAULT_FILE_FILE REFERENCES FILE_STORAGE (file_storage_fileid),
        customer_vault_file_title NVARCHAR(300) NOT NULL,                 -- Ten khach dat cho tai lieu
        customer_vault_file_note NVARCHAR(1000) NULL,                     -- Ghi chu, tu khoa khach tu them
        customer_vault_file_ocr_status VARCHAR(20) NOT NULL CONSTRAINT DF_CustomerVaultFile_Ocr DEFAULT 'QUEUED', -- QUEUED, PROCESSING, SUCCESS, FAILED, INTERRUPTED
        customer_vault_file_page_count INT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_CustomerVaultFile_CreatedAt DEFAULT SYSUTCDATETIME(),
        deleted_at DATETIME2 NULL,                                        -- Khach da xoa: tep tren o dia da bo
        CONSTRAINT CK_CustomerVaultFile_Ocr CHECK (customer_vault_file_ocr_status IN ('QUEUED', 'PROCESSING', 'SUCCESS', 'FAILED', 'INTERRUPTED'))
    );
    CREATE INDEX IX_CustomerVaultFile_Customer ON CUSTOMER_VAULT_FILE (customer_id, deleted_at);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_VAULT_KEYWORD')
BEGIN
    CREATE TABLE CUSTOMER_VAULT_KEYWORD (
        customer_vault_keyword_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CUSTOMER_VAULT_KEYWORD PRIMARY KEY,
        customer_vault_file_id BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_VAULT_KEYWORD_FILE REFERENCES CUSTOMER_VAULT_FILE (customer_vault_file_id),
        customer_vault_keyword_type INT NOT NULL,                         -- 1 so hop dong, 2 MST, 3 SDT, 4 ten, 5 email (nhu CONTRACT_SCAN_KEYWORD)
        customer_vault_keyword_value NVARCHAR(400) NOT NULL,
        CONSTRAINT CK_CustomerVaultKeyword_Type CHECK (customer_vault_keyword_type BETWEEN 1 AND 5)
    );
    CREATE UNIQUE INDEX UX_CustomerVaultKeyword_File_Value ON CUSTOMER_VAULT_KEYWORD (customer_vault_file_id, customer_vault_keyword_type, customer_vault_keyword_value);
END
GO
