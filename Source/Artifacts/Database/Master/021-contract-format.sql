SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 021: THIET LAP THAM SO HOP DONG (CONTRACT_FORMAT_SETTING). Chay sau 020; chay lai nhieu lan khong sao.
-- Mot dong (setting_id = 1) giu phong chu, co chu, le trang, gian dong cua hop dong do he thong ve (JSON cua ContractFormat).
-- Chua co dong nao thi he thong dung dinh dang mac dinh nhu truoc. Mau Word giu dinh dang cua file.
IF OBJECT_ID('dbo.CONTRACT_FORMAT_SETTING', 'U') IS NULL
    CREATE TABLE dbo.CONTRACT_FORMAT_SETTING (
        setting_id INT NOT NULL CONSTRAINT PK_CONTRACT_FORMAT_SETTING PRIMARY KEY,
        setting_value NVARCHAR(2000) NOT NULL,
        updated_date DATETIME2 NOT NULL CONSTRAINT DF_CONTRACT_FORMAT_SETTING_UPDATED DEFAULT SYSUTCDATETIME(),
        updated_employee_id BIGINT NULL,
        CONSTRAINT CK_CONTRACT_FORMAT_SETTING_SINGLE CHECK (setting_id = 1)
    );
GO
