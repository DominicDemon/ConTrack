-- 006: TỪ KHÓA OCR CỦA TỆP BẢN KÝ GIẤY, DÙNG ĐỂ ĐỐI CHIẾU VÀ TÌM KIẾM. BẢNG RIÊNG CỦA NHÓM TRÊN DATABASE DEV CLONE.
-- Bối cảnh (Master_Database_HuongDan F18, F19): bản ký giấy là FILE_STORAGE + CONTRACT_DOCUMENT_FILES (đúng phiên bản,
-- vai trò bản ký) + CONTRACT_SIGNED_EVIDENCE. Hệ thống đọc OCR tệp đó ở chế độ nền, tách số hợp đồng, mã số thuế,
-- số điện thoại, tên khách hàng để đối chiếu với hồ sơ trước khi xác nhận (F18 bước 1) và để tìm lại hợp đồng sau này.
-- MasterDB-Optimize_v1.sql có OCR_JOB (lưu toàn văn ocr_text) nhưng chưa có chỗ lưu từ khóa để tìm kiếm có chỉ mục.
-- Từ khóa gắn theo tệp (file_storage_fileid); từ tệp truy được về bản ký, phiên bản và hồ sơ qua CONTRACT_DOCUMENT_FILES.
-- Theo đúng quy ước của MasterDB-Optimize.sql: tên bảng in hoa, cột có tiền tố tên bảng, không khai báo FOREIGN KEY.
-- Không lưu toàn bộ văn bản OCR: văn bản có thể đọc sai và chứa số tài khoản, địa chỉ của khách.

-- Bản đề xuất trước gắn từ khóa với CONTRACT_SIGNED_EVIDENCE. Database dev nào đã chạy bản đó thì tạo lại 2 bảng
-- (dữ liệu trong đó chỉ là dữ liệu thử).
IF COL_LENGTH('CONTRACT_SCAN_KEYWORD', 'contract_signed_evidence_id') IS NOT NULL
    DROP TABLE CONTRACT_SCAN_KEYWORD;
IF COL_LENGTH('CONTRACT_SCAN_KEYWORD_JOB', 'contract_signed_evidence_id') IS NOT NULL
    DROP TABLE CONTRACT_SCAN_KEYWORD_JOB;
GO

-- ===========================================================================================================
-- TỪ KHÓA TÌM KIẾM CỦA TỪNG BẢN QUÉT
-- ===========================================================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_SCAN_KEYWORD')
BEGIN
    CREATE TABLE CONTRACT_SCAN_KEYWORD (
        contract_scan_keyword_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_SCAN_KEYWORD PRIMARY KEY,
        file_storage_fileid BIGINT NOT NULL,                        -- Tệp bản ký (FILE_STORAGE), qua CONTRACT_DOCUMENT_FILES tới bản ký và phiên bản
        contract_scan_keyword_type INT NOT NULL,                    -- 1 = Số hợp đồng, 2 = Mã số thuế, 3 = Số điện thoại, 4 = Tên khách hàng
        contract_scan_keyword_value NVARCHAR(400) NOT NULL,         -- Số hợp đồng in hoa không dấu; mã số thuế, số điện thoại chỉ gồm chữ số; tên khách hàng giữ nguyên như trên giấy
        contract_scan_keyword_pagenumber INT NULL,                  -- Trang đầu tiên có từ khóa (bắt đầu từ 1); NULL với từ khóa thêm bằng tay
        contract_scan_keyword_source INT NOT NULL CONSTRAINT DF_ContractScanKeyword_Source DEFAULT 1, -- 1 = OCR đọc, 2 = Nhân viên thêm bằng tay (khi OCR đọc thiếu)
        created_by_employee_id BIGINT NULL,                         -- Nhân viên thêm bằng tay; NULL với từ khóa OCR đọc
        created_at DATETIME2 NOT NULL CONSTRAINT DF_ContractScanKeyword_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_ContractScanKeyword_Type CHECK (contract_scan_keyword_type BETWEEN 1 AND 4),
        -- OCR biết trang và không có người thêm; từ khóa thêm tay thì ngược lại.
        -- Đọc lại OCR chỉ thay các từ khóa nguồn 1, từ khóa thêm tay giữ nguyên.
        CONSTRAINT CK_ContractScanKeyword_Source CHECK (
            (contract_scan_keyword_source = 1 AND contract_scan_keyword_pagenumber > 0 AND created_by_employee_id IS NULL)
            OR (contract_scan_keyword_source = 2 AND contract_scan_keyword_pagenumber IS NULL AND created_by_employee_id IS NOT NULL)
        )
    );

    -- Đọc lại một bản quét không tạo dòng trùng.
    CREATE UNIQUE INDEX UX_ContractScanKeyword_File_Value
        ON CONTRACT_SCAN_KEYWORD (file_storage_fileid, contract_scan_keyword_type, contract_scan_keyword_value);
    CREATE INDEX IX_ContractScanKeyword_Value
        ON CONTRACT_SCAN_KEYWORD (contract_scan_keyword_type, contract_scan_keyword_value);
END
GO

-- Hàng đợi OCR dùng OCR_BATCH và OCR_JOB của MasterDB-Optimize_v1.sql (mỗi lần tải lên 1 batch 1 tệp).
-- Bảng hàng đợi riêng CONTRACT_SCAN_KEYWORD_JOB của bản đề xuất trước không dùng nữa.
IF OBJECT_ID('CONTRACT_SCAN_KEYWORD_JOB', 'U') IS NOT NULL
    DROP TABLE CONTRACT_SCAN_KEYWORD_JOB;
GO
