-- 008: MÃ HÓA TỆP BẢN SCAN BẰNG KEY 12 KÝ TỰ CẤP CHO TỪNG NGƯỜI DÙNG, CÓ BẢN DỰ PHÒNG. BẢNG RIÊNG CỦA NHÓM TRÊN DATABASE DEV CLONE.
-- Cách làm (mã hóa hai lớp):
--   * Mỗi tệp có một khóa ngẫu nhiên 256 bit; tệp được mã hóa AES-256-GCM rồi mới ghi vào nơi lưu tệp.
--   * Mỗi nhân viên được cấp một key 12 ký tự (hiện một lần cho người cấp, không lưu trong database) và một cặp khóa RSA:
--     khóa công khai lưu thẳng, khóa bí mật được mã hóa bằng khóa dẫn xuất từ key 12 ký tự (PBKDF2-SHA256).
--   * Khóa của tệp được bọc bằng khóa công khai của từng người được xem tệp, và bằng khóa công khai dự phòng của công ty
--     (khóa bí mật dự phòng KHÔNG nằm trên máy chủ ứng dụng hay trong database).
-- Database chỉ giữ khóa đã bọc: lộ database và nơi lưu tệp cũng không mở được tệp nếu không có key của người dùng
-- hoặc khóa bí mật dự phòng.
-- Theo quy ước của MasterDB-Optimize_v1.sql: tên bảng in hoa, cột có tiền tố tên bảng, không khai báo FOREIGN KEY.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EMPLOYEE_ENCRYPTION_KEY')
BEGIN
    CREATE TABLE EMPLOYEE_ENCRYPTION_KEY (
        employee_encryption_key_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EMPLOYEE_ENCRYPTION_KEY PRIMARY KEY,
        employee_id BIGINT NOT NULL,                                        -- Nhân viên (EMPLOYEE) được cấp key
        employee_encryption_key_publickey VARBINARY(MAX) NOT NULL,          -- Khóa công khai RSA (SubjectPublicKeyInfo)
        employee_encryption_key_privatekey VARBINARY(MAX) NOT NULL,         -- Khóa bí mật RSA đã mã hóa AES-256-GCM bằng khóa dẫn xuất từ key 12 ký tự
        employee_encryption_key_salt VARBINARY(32) NOT NULL,                -- Salt của PBKDF2
        employee_encryption_key_iterations INT NOT NULL,                    -- Số vòng PBKDF2-SHA256
        employee_encryption_key_status VARCHAR(20) NOT NULL CONSTRAINT DF_EmployeeEncryptionKey_Status DEFAULT 'ACTIVE', -- ACTIVE, REVOKED
        issued_by_employee_id BIGINT NOT NULL,                              -- Người cấp key
        issued_at DATETIME2 NOT NULL CONSTRAINT DF_EmployeeEncryptionKey_IssuedAt DEFAULT SYSUTCDATETIME(),
        revoked_at DATETIME2 NULL,                                          -- Bị thay bằng key mới hoặc thu hồi
        CONSTRAINT CK_EmployeeEncryptionKey_Status CHECK (employee_encryption_key_status IN ('ACTIVE', 'REVOKED'))
    );

    -- Mỗi nhân viên chỉ có một key đang dùng.
    CREATE UNIQUE INDEX UX_EmployeeEncryptionKey_ActiveEmployee
        ON EMPLOYEE_ENCRYPTION_KEY (employee_id) WHERE employee_encryption_key_status = 'ACTIVE';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FILE_ENCRYPTION_KEY')
BEGIN
    CREATE TABLE FILE_ENCRYPTION_KEY (
        file_encryption_key_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FILE_ENCRYPTION_KEY PRIMARY KEY,
        file_storage_fileid BIGINT NOT NULL,                                -- Tệp đã mã hóa (FILE_STORAGE)
        employee_encryption_key_id BIGINT NULL,                             -- Key của người được mở tệp; NULL là bản dự phòng của công ty
        file_encryption_key_wrappedkey VARBINARY(MAX) NOT NULL,             -- Khóa của tệp, bọc RSA-OAEP-SHA256 bằng khóa công khai tương ứng
        file_encryption_key_recoverykeyid VARCHAR(64) NULL,                 -- Dấu SHA-256 của khóa công khai dự phòng đã dùng (chỉ với bản dự phòng)
        granted_by_employee_id BIGINT NULL,                                 -- Người cấp quyền mở; NULL khi hệ thống tự cấp lúc tải lên
        granted_at DATETIME2 NOT NULL CONSTRAINT DF_FileEncryptionKey_GrantedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_FileEncryptionKey_File_Key
        ON FILE_ENCRYPTION_KEY (file_storage_fileid, employee_encryption_key_id) WHERE employee_encryption_key_id IS NOT NULL;
    CREATE UNIQUE INDEX UX_FileEncryptionKey_File_Recovery
        ON FILE_ENCRYPTION_KEY (file_storage_fileid) WHERE employee_encryption_key_id IS NULL;
    CREATE INDEX IX_FileEncryptionKey_Key
        ON FILE_ENCRYPTION_KEY (employee_encryption_key_id);
END
GO
