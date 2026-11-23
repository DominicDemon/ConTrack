-- 023: KHO LUU TRU CUA KHACH - BO SUNG. Chay sau 022; chay lai nhieu lan khong sao.
-- Phan loai (thu muc), ngay het han va nhac han, thung rac (giu 30 ngay roi moi xoa han),
-- va tu khoa khach tu nhap (nguon 2) duoc giu lai khi doc lai OCR (nguon 1).

IF COL_LENGTH('CUSTOMER_VAULT_FILE', 'customer_vault_file_category') IS NULL
    ALTER TABLE CUSTOMER_VAULT_FILE ADD customer_vault_file_category NVARCHAR(100) NULL;   -- Thu muc / loai tai lieu
IF COL_LENGTH('CUSTOMER_VAULT_FILE', 'customer_vault_file_expires_on') IS NULL
    ALTER TABLE CUSTOMER_VAULT_FILE ADD customer_vault_file_expires_on DATE NULL;           -- Ngay het han hop dong cua khach
IF COL_LENGTH('CUSTOMER_VAULT_FILE', 'customer_vault_file_remind_days') IS NULL
    ALTER TABLE CUSTOMER_VAULT_FILE ADD customer_vault_file_remind_days INT NULL;           -- Nhac truoc bao nhieu ngay (mac dinh 30)
IF COL_LENGTH('CUSTOMER_VAULT_FILE', 'customer_vault_file_reminded_days') IS NULL
    ALTER TABLE CUSTOMER_VAULT_FILE ADD customer_vault_file_reminded_days INT NULL;         -- Moc nhac da gui gan nhat (so ngay con lai)
IF COL_LENGTH('CUSTOMER_VAULT_FILE', 'trashed_at') IS NULL
    ALTER TABLE CUSTOMER_VAULT_FILE ADD trashed_at DATETIME2 NULL;                          -- Trong thung rac tu luc nay; sau 30 ngay xoa han
GO

IF COL_LENGTH('CUSTOMER_VAULT_KEYWORD', 'customer_vault_keyword_source') IS NULL
    ALTER TABLE CUSTOMER_VAULT_KEYWORD ADD customer_vault_keyword_source INT NOT NULL
        CONSTRAINT DF_CustomerVaultKeyword_Source DEFAULT 1;                                  -- 1 OCR doc, 2 khach tu nhap
GO
