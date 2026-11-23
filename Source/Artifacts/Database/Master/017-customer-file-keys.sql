SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 017: KHACH HANG XEM BAN SCAN HOP DONG CUA MINH BANG KEY RIENG. Chay sau 008; chay lai nhieu lan khong sao.
-- Khach tu dat key tren cong khach hang (mat khau + ma gui qua email), giong nhan vien (008, 014): khoa bi mat RSA
-- cua khach duoc ma hoa bang key do, he thong khong luu key. Moi ban scan cua khach duoc boc khoa tep rieng cho key
-- cua khach trong CUSTOMER_FILE_KEY; bang FILE_ENCRYPTION_KEY cua nhan vien va ban du phong giu nguyen.
-- Ban scan cua khach: ban ky giay (luong A) da xac nhan Hop le cua hop dong cua khach, va hop dong giay scan
-- (luong B) da gan cho khach hoac co ma so thue / so dien thoai / email trung voi khach (tu khoa OCR).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_ENCRYPTION_KEY')
BEGIN
    CREATE TABLE CUSTOMER_ENCRYPTION_KEY (
        customer_encryption_key_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CUSTOMER_ENCRYPTION_KEY PRIMARY KEY,
        customer_id BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_ENCRYPTION_KEY_CUSTOMER REFERENCES CUSTOMER (customer_id),
        customer_encryption_key_publickey VARBINARY(MAX) NOT NULL,     -- Khoa cong khai RSA (SubjectPublicKeyInfo)
        customer_encryption_key_privatekey VARBINARY(MAX) NOT NULL,    -- Khoa bi mat RSA, ma hoa AES-256-GCM bang khoa dan xuat tu key cua khach
        customer_encryption_key_salt VARBINARY(32) NOT NULL,           -- Salt cua PBKDF2
        customer_encryption_key_iterations INT NOT NULL,               -- So vong PBKDF2-SHA256
        customer_encryption_key_status VARCHAR(20) NOT NULL CONSTRAINT DF_CustomerEncryptionKey_Status DEFAULT 'ACTIVE', -- ACTIVE, REVOKED
        created_at DATETIME2 NOT NULL CONSTRAINT DF_CustomerEncryptionKey_CreatedAt DEFAULT SYSUTCDATETIME(), -- Luc dat hoac doi key
        revoked_at DATETIME2 NULL,
        CONSTRAINT CK_CustomerEncryptionKey_Status CHECK (customer_encryption_key_status IN ('ACTIVE', 'REVOKED'))
    );
END
GO
-- Separate guard: a run that stopped after creating the table still gets its index.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_CustomerEncryptionKey_ActiveCustomer')
    CREATE UNIQUE INDEX UX_CustomerEncryptionKey_ActiveCustomer
        ON CUSTOMER_ENCRYPTION_KEY (customer_id) WHERE customer_encryption_key_status = 'ACTIVE';
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_FILE_KEY')
BEGIN
    CREATE TABLE CUSTOMER_FILE_KEY (
        customer_file_key_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CUSTOMER_FILE_KEY PRIMARY KEY,
        file_storage_fileid BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_FILE_KEY_FILE REFERENCES FILE_STORAGE (file_storage_fileid),
        customer_encryption_key_id BIGINT NOT NULL CONSTRAINT FK_CUSTOMER_FILE_KEY_KEY REFERENCES CUSTOMER_ENCRYPTION_KEY (customer_encryption_key_id),
        customer_file_key_wrappedkey VARBINARY(MAX) NOT NULL,          -- Khoa cua tep, boc RSA-OAEP-SHA256 bang khoa cong khai cua khach
        granted_at DATETIME2 NOT NULL CONSTRAINT DF_CustomerFileKey_GrantedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_CustomerFileKey_File_Key ON CUSTOMER_FILE_KEY (file_storage_fileid, customer_encryption_key_id);
    CREATE INDEX IX_CustomerFileKey_Key ON CUSTOMER_FILE_KEY (customer_encryption_key_id);
END
GO
