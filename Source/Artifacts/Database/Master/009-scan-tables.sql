-- 009: CÁC BẢNG CỦA MasterDB-Optimize_v1 MÀ PHẦN BẢN SCAN DÙNG NHƯNG MasterDB-Optimize / MasterDB-ConTrack CHƯA CÓ.
-- CONTRACT_TYPES, CONTRACTS (hợp đồng giấy scan: source_type = 'SCANNED'), OCR_BATCH, OCR_JOB (hàng đợi đọc OCR).
-- Cột và kiểu giữ đúng như MasterDB-Optimize_v1.sql; chỉ đặt tên khóa chính rõ ràng như MasterDB-Optimize.sql.
-- Chỉ tạo bảng còn thiếu, không đổi bảng hay dữ liệu đã có, chạy lại nhiều lần được.
-- Thứ tự chạy: 001 -> 009.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_TYPES')
BEGIN
    CREATE TABLE CONTRACT_TYPES (
        contract_type_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_TYPES PRIMARY KEY,
        contract_type_code VARCHAR(50) NOT NULL CONSTRAINT UQ_CONTRACT_TYPES_Code UNIQUE,
        contract_type_name NVARCHAR(500) NOT NULL,
        contract_type_nameen NVARCHAR(500) NULL,
        contract_type_description NVARCHAR(2000) NULL,
        contract_status NVARCHAR(200) NULL,
        default_duration_months INT NULL,
        created_date DATETIME2 NOT NULL CONSTRAINT DF_CONTRACT_TYPES_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_date DATETIME2 NULL,
        created_employee_id INT NULL,
        modified_employee_id INT NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACTS')
BEGIN
    CREATE TABLE CONTRACTS (
        contract_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACTS PRIMARY KEY,
        contract_type_id INT NULL,
        contract_code VARCHAR(50) NOT NULL CONSTRAINT UQ_CONTRACTS_Code UNIQUE,
        [contract_name] NVARCHAR(500) NOT NULL,
        contract_nameen NVARCHAR(500) NULL,
        partner_id BIGINT NULL,
        employee_id BIGINT NULL,
        department_id INT NULL,
        source_type VARCHAR(10) NULL,                -- ONLINE / SCANNED
        contract_status NVARCHAR(200) NULL,          -- Vòng đời hợp đồng
        approval_status NVARCHAR(200) NULL,
        signed_date DATETIME NULL,
        effective_date DATETIME NULL,
        expiration_date DATETIME NULL,
        currency VARCHAR(10) NULL,
        contract_value FLOAT NULL,
        payment_terms NVARCHAR(MAX) NULL,
        contract_description NVARCHAR(2000) NULL,
        file_storage_fileid BIGINT NULL,             -- Bản scan của hợp đồng giấy
        default_duration_months INT NULL,
        created_date DATETIME2 NOT NULL CONSTRAINT DF_CONTRACTS_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_date DATETIME2 NULL,
        created_employee_id INT NULL,
        modified_employee_id INT NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OCR_BATCH')
BEGIN
    CREATE TABLE OCR_BATCH (
        ocr_patch_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OCR_BATCH PRIMARY KEY,
        uploaded_by_employee_id BIGINT NOT NULL,
        total_files INT NOT NULL,
        batch_status VARCHAR(30) NOT NULL CONSTRAINT DF_OCR_BATCH_Status DEFAULT 'PENDING', -- PENDING, PROCESSING, COMPLETED, FAILED
        created_at DATETIME2 NOT NULL,
        updated_by_employee_id BIGINT NULL,
        updated_at DATETIME NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OCR_JOB')
BEGIN
    CREATE TABLE OCR_JOB (
        ocr_job_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OCR_JOB PRIMARY KEY,
        ocr_batch_id BIGINT NOT NULL,
        file_storage_fileid BIGINT NOT NULL,
        contract_document_id BIGINT NULL,                            -- NULL với hợp đồng giấy scan (không có CONTRACT_DOCUMENTS)
        ocr_job_avg_confidence_score DECIMAL(5,2) NULL,              -- Độ chính xác trung bình (%)
        ocr_job_processing_status VARCHAR(30) NOT NULL CONSTRAINT DF_OCR_JOB_Status DEFAULT 'QUEUED', -- QUEUED, PROCESSING, SUCCESS, FAILED, NEED_REVIEW
        ocr_text NVARCHAR(MAX) NULL,
        page_count INT NULL,
        scan_quality NVARCHAR(100) NULL,
        error_message NVARCHAR(MAX) NULL,
        scanned_employee_id BIGINT NULL,
        scanned_at DATETIME2 NOT NULL CONSTRAINT DF_OCR_JOB_ScannedAt DEFAULT SYSUTCDATETIME(),
        completed_at DATETIME2 NULL
    );
END;

-- Hàng đợi OCR lấy việc theo trạng thái; tìm bản scan theo tệp.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OCR_JOB') AND name = 'IX_OCR_JOB_Status')
    CREATE INDEX IX_OCR_JOB_Status ON dbo.OCR_JOB(ocr_job_processing_status, scanned_at);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OCR_JOB') AND name = 'IX_OCR_JOB_File')
    CREATE INDEX IX_OCR_JOB_File ON dbo.OCR_JOB(file_storage_fileid);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CONTRACTS') AND name = 'IX_CONTRACTS_File')
    CREATE INDEX IX_CONTRACTS_File ON dbo.CONTRACTS(file_storage_fileid);

COMMIT TRANSACTION;
