-- 024: NHAN PHAN LOAI BAN SCAN. Chay sau 009; chay lai nhieu lan khong sao.
-- Thay cho viec luu ban scan vao thu muc tren may (ra khoi lop ma hoa, moi nguoi xep mot kieu): nhan dung chung
-- trong cong ty, mot ban scan gan duoc nhieu nhan, loc danh sach theo nhan. Chi gan cho hop dong giay scan
-- (OCR_JOB khong co CONTRACT_DOCUMENTS); quyen kiem tra trong ung dung (contract.scanned.*).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SCAN_LABEL')
BEGIN
    CREATE TABLE SCAN_LABEL (
        scan_label_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SCAN_LABEL PRIMARY KEY,
        scan_label_name NVARCHAR(60) COLLATE Latin1_General_100_CI_AI NOT NULL,  -- "Kho Binh Duong" trung "kho bình dương"
        scan_label_color VARCHAR(20) NOT NULL CONSTRAINT DF_ScanLabel_Color DEFAULT 'slate',
        created_by_employee_id BIGINT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_ScanLabel_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_ScanLabel_Color CHECK (scan_label_color IN ('slate', 'blue', 'green', 'amber', 'red', 'violet', 'teal', 'pink'))
    );
    CREATE UNIQUE INDEX UX_ScanLabel_Name ON SCAN_LABEL (scan_label_name);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SCAN_LABEL_FILE')
BEGIN
    CREATE TABLE SCAN_LABEL_FILE (
        scan_label_id BIGINT NOT NULL CONSTRAINT FK_SCAN_LABEL_FILE_LABEL REFERENCES SCAN_LABEL (scan_label_id),
        file_storage_fileid BIGINT NOT NULL CONSTRAINT FK_SCAN_LABEL_FILE_FILE REFERENCES FILE_STORAGE (file_storage_fileid),
        assigned_by_employee_id BIGINT NULL,
        assigned_at DATETIME2 NOT NULL CONSTRAINT DF_ScanLabelFile_AssignedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SCAN_LABEL_FILE PRIMARY KEY (scan_label_id, file_storage_fileid)
    );
    CREATE INDEX IX_ScanLabelFile_File ON SCAN_LABEL_FILE (file_storage_fileid);
END
GO
