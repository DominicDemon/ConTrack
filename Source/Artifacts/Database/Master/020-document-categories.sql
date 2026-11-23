SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 020: LOAI VAN BAN (DOCUMENT_CATEGORY). Chay sau 019; chay lai nhieu lan khong sao.
-- Hanh chinh tu them loai van ban (bien ban nghiem thu, thong bao...). Moi loai mang quy tac gui khach rieng:
-- ten va tinh bat buoc cua hai ngay, bang hang muc, so tien, lich thanh toan, dot thanh toan cua hop dong.
-- 1..5 la cac loai co san (cung so voi CONTRACT_TEMPLATE.contract_template_document_type); loai 1 (Hop dong) co dinh.
-- Chay bang: sqlcmd ... -f 65001 -i 020-document-categories.sql

IF OBJECT_ID('dbo.DOCUMENT_CATEGORY', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DOCUMENT_CATEGORY (
        document_category_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DOCUMENT_CATEGORY PRIMARY KEY,
        document_category_name NVARCHAR(200) NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_DOCUMENT_CATEGORY_ACTIVE DEFAULT 1,
        effective_label NVARCHAR(100) NOT NULL,
        effective_required BIT NOT NULL,
        expire_shown BIT NOT NULL,
        expire_label NVARCHAR(100) NOT NULL CONSTRAINT DF_DOCUMENT_CATEGORY_EXPIRE_LABEL DEFAULT N'',
        expire_required BIT NOT NULL,
        items_label NVARCHAR(100) NOT NULL,
        items_required BIT NOT NULL,
        item_prices BIT NOT NULL,
        amount_required BIT NOT NULL,
        milestones BIT NOT NULL,
        milestones_required BIT NULL, -- NULL: chi can lich thanh toan khi van ban co hang muc
        parent_milestone BIT NOT NULL,
        created_date DATETIME2 NOT NULL CONSTRAINT DF_DOCUMENT_CATEGORY_CREATED DEFAULT SYSUTCDATETIME(),
        updated_date DATETIME2 NULL,
        CONSTRAINT UQ_DOCUMENT_CATEGORY_NAME UNIQUE (document_category_name)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.DOCUMENT_CATEGORY)
BEGIN
    SET IDENTITY_INSERT dbo.DOCUMENT_CATEGORY ON;
    INSERT dbo.DOCUMENT_CATEGORY (document_category_id, document_category_name, effective_label, effective_required, expire_shown, expire_label, expire_required,
        items_label, items_required, item_prices, amount_required, milestones, milestones_required, parent_milestone)
    VALUES
        (1, N'Hợp đồng', N'Ngày hiệu lực', 0, 1, N'Ngày hết hạn', 1, N'Hạng mục hợp đồng', 1, 1, 0, 1, 1, 0),
        (2, N'Phụ lục', N'Ngày hiệu lực', 1, 1, N'Ngày hết hạn', 0, N'Hạng mục điều chỉnh', 0, 1, 0, 1, NULL, 0),
        (3, N'Biên bản bàn giao', N'Ngày bàn giao', 1, 0, N'', 0, N'Danh mục bàn giao', 1, 0, 0, 0, 0, 0),
        (4, N'Biên lai', N'Ngày thu', 1, 0, N'', 0, N'Khoản thu', 1, 1, 1, 0, 0, 0),
        (5, N'Đề nghị thanh toán', N'Ngày đề nghị', 0, 1, N'Hạn thanh toán', 1, N'Khoản đề nghị thanh toán', 1, 1, 1, 0, 0, 1);
    SET IDENTITY_INSERT dbo.DOCUMENT_CATEGORY OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CONTRACT_TEMPLATE_DOCUMENT_CATEGORY')
    ALTER TABLE dbo.CONTRACT_TEMPLATE WITH CHECK ADD CONSTRAINT FK_CONTRACT_TEMPLATE_DOCUMENT_CATEGORY
        FOREIGN KEY (contract_template_document_type) REFERENCES dbo.DOCUMENT_CATEGORY (document_category_id);
GO
