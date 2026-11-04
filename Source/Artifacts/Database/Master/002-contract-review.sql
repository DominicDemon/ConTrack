-- 002: luồng "gửi khách xem hợp đồng nháp" (CONTRACT-REVIEW-FLOW). Chỉ thêm, không xóa/đổi tên.
-- Nguồn: phần [THÊM-xx] của MasterDB-ConTrack.sql. Idempotent; bootstrap tách batch theo dòng GO
-- và chạy tất cả trong một transaction.

-- [THÊM-02] 0 Draft, 1 Negotiating, 2 PendingApproval, 3 PendingSignature, 4 Signed, 5 Completed, 6 Cancelled, 7 Rejected
IF OBJECT_ID('CK_Doc_Status', 'C') IS NULL
    ALTER TABLE CONTRACT_DOCUMENTS ADD CONSTRAINT CK_Doc_Status CHECK (contract_document_status BETWEEN 0 AND 7);
GO

-- [THÊM-04]
IF COL_LENGTH('dbo.CONTRACT_NEGOTIATION_COMMENT', 'author_customer_id') IS NULL
    ALTER TABLE CONTRACT_NEGOTIATION_COMMENT ADD author_customer_id BIGINT NULL;
IF COL_LENGTH('dbo.CONTRACT_NEGOTIATION_COMMENT', 'contract_review_request_id') IS NULL
    ALTER TABLE CONTRACT_NEGOTIATION_COMMENT ADD contract_review_request_id BIGINT NULL;
GO

-- [THÊM-05]
IF COL_LENGTH('dbo.CONTRACT_APPROVAL_REQUEST', 'contract_review_request_id') IS NULL
    ALTER TABLE CONTRACT_APPROVAL_REQUEST ADD contract_review_request_id BIGINT NULL;
GO

-- [THÊM-06] Khóa ngoại ghép (document_id, version_id) → bảo đảm phiên bản thuộc đúng hồ sơ
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'UQ_ContractVersion_Document_Version')
    ALTER TABLE CONTRACT_VERSION ADD CONSTRAINT UQ_ContractVersion_Document_Version UNIQUE (contract_document_id, contract_version_id);
GO

-- ----------------------------------------------------------------------------------------------------
-- [THÊM-07] HÀNG ĐỢI SMS (outbox) — cùng pattern với tbl_ContractCustomerOtpDeliveryOutbox / AccountRecoveryEmailOutbox
-- Worker lấy dòng PENDING (lease), giải mã, gọi eSMS SendMultipleMessage_V4_post_json, ghi kết quả, XÓA ciphertext.
-- ----------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SMS_OUTBOX')
BEGIN
    CREATE TABLE SMS_OUTBOX (
        sms_outbox_id                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SMS_OUTBOX PRIMARY KEY,
        sms_outbox_purpose            VARCHAR(30)    NOT NULL,          -- CUSTOMER_TEMP_PASSWORD, CONTRACT_REVIEW_NOTICE, CONTRACT_SIGN_OTP
        customer_id                   BIGINT         NULL,
        contract_document_id          BIGINT         NULL,
        sms_outbox_phone_ciphertext   VARBINARY(512) NULL,              -- SĐT mã hóa; xóa sau khi gửi xong / hết hạn
        sms_outbox_content_ciphertext VARBINARY(2048) NULL,             -- nội dung có mật khẩu tạm / OTP, mã hóa; xóa sau khi gửi
        sms_outbox_phone_masked       VARCHAR(20)    NOT NULL,          -- 090****484 để hiển thị / tra cứu
        sms_outbox_delivery_expires_at DATETIME2     NOT NULL,          -- quá hạn mà chưa gửi được thì bỏ (mật khẩu/OTP đã hết hạn)

        -- Hàng đợi
        sms_outbox_status             VARCHAR(20)    NOT NULL CONSTRAINT DF_SmsOutbox_Status DEFAULT 'PENDING',
                                      -- PENDING, PROCESSING, ACCEPTED (eSMS CodeResult=100), DELIVERED, FAILED, EXPIRED
        sms_outbox_attempt_count      INT            NOT NULL CONSTRAINT DF_SmsOutbox_AttemptCount DEFAULT 0,
        sms_outbox_next_attempt_at    DATETIME2      NULL,
        sms_outbox_lease_id           UNIQUEIDENTIFIER NULL,
        sms_outbox_lease_until        DATETIME2      NULL,
        sms_outbox_sent_at            DATETIME2      NULL,
        sms_outbox_failed_at          DATETIME2      NULL,
        sms_outbox_last_failure       NVARCHAR(1000) NULL,

        -- Dữ liệu eSMS
        sms_outbox_provider           VARCHAR(30)    NOT NULL CONSTRAINT DF_SmsOutbox_Provider DEFAULT 'ESMS',
        sms_outbox_request_id         VARCHAR(50)    NOT NULL,          -- RequestId (<= 50 ký tự, eSMS chặn trùng 24h)
        sms_outbox_brandname          VARCHAR(50)    NULL,
        sms_outbox_is_sandbox         BIT            NOT NULL CONSTRAINT DF_SmsOutbox_IsSandbox DEFAULT 0,
        sms_outbox_code_result        VARCHAR(10)    NULL,              -- CodeResult: 100, 101, 103, 104, 124, 146...
        sms_outbox_provider_sms_id    VARCHAR(100)   NULL,              -- SMSID
        sms_outbox_send_status        VARCHAR(5)     NULL,              -- SendStatus callback: 1, 2, 4, 5, 7
        sms_outbox_send_success       INT            NULL,
        sms_outbox_send_failed        INT            NULL,
        sms_outbox_telco_id           VARCHAR(10)    NULL,
        sms_outbox_total_price        DECIMAL(18,4)  NULL,
        sms_outbox_delivered_at       DATETIME2      NULL,

        created_at                    DATETIME2      NOT NULL CONSTRAINT DF_SmsOutbox_CreatedAt DEFAULT SYSUTCDATETIME(),
        rowversion                    ROWVERSION     NOT NULL,
        CONSTRAINT UQ_SmsOutbox_RequestId UNIQUE (sms_outbox_request_id),
        CONSTRAINT CK_SmsOutbox_Purpose CHECK (sms_outbox_purpose IN ('CUSTOMER_TEMP_PASSWORD','CONTRACT_REVIEW_NOTICE','CONTRACT_SIGN_OTP')),
        CONSTRAINT CK_SmsOutbox_Status CHECK (sms_outbox_status IN ('PENDING','PROCESSING','ACCEPTED','DELIVERED','FAILED','EXPIRED')),
        CONSTRAINT FK_SmsOutbox_Customer FOREIGN KEY (customer_id) REFERENCES CUSTOMER (customer_id),
        CONSTRAINT FK_SmsOutbox_Document FOREIGN KEY (contract_document_id) REFERENCES CONTRACT_DOCUMENTS (contract_document_id)
    );
    CREATE INDEX IX_SmsOutbox_Pending ON SMS_OUTBOX (sms_outbox_status, sms_outbox_next_attempt_at)
        WHERE sms_outbox_status IN ('PENDING','PROCESSING');
    CREATE INDEX IX_SmsOutbox_ProviderSmsId ON SMS_OUTBOX (sms_outbox_provider, sms_outbox_provider_sms_id);
    CREATE INDEX IX_SmsOutbox_Customer_Purpose_CreatedAt ON SMS_OUTBOX (customer_id, sms_outbox_purpose, created_at); -- giới hạn tần suất
END;
GO

-- [THÊM-08] Log thô callback eSMS (GET query string, gửi lại tối đa 5 lần, không có chữ ký → kiểm tra IP nguồn)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SMS_PROVIDER_CALLBACK')
BEGIN
    CREATE TABLE SMS_PROVIDER_CALLBACK (
        sms_provider_callback_id      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SMS_PROVIDER_CALLBACK PRIMARY KEY,
        sms_outbox_id                 BIGINT         NULL,
        sms_provider_callback_provider_sms_id VARCHAR(100) NULL,
        sms_provider_callback_request_id VARCHAR(50) NULL,
        sms_provider_callback_send_status VARCHAR(5) NULL,
        sms_provider_callback_raw_query NVARCHAR(4000) NOT NULL,
        sms_provider_callback_source_ip VARCHAR(45)  NULL,              -- eSMS: 103.29.26.39, 103.29.26.80, 103.29.27.39
        sms_provider_callback_is_processed BIT       NOT NULL CONSTRAINT DF_SmsCallback_IsProcessed DEFAULT 0,
        received_at                   DATETIME2      NOT NULL CONSTRAINT DF_SmsCallback_ReceivedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_SmsCallback_SmsOutbox FOREIGN KEY (sms_outbox_id) REFERENCES SMS_OUTBOX (sms_outbox_id)
    );
    CREATE INDEX IX_SmsCallback_SmsOutbox ON SMS_PROVIDER_CALLBACK (sms_outbox_id);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- [THÊM-10] MỘT LẦN "GỬI KHÁCH XEM" MỘT PHIÊN BẢN + PHẢN HỒI CỦA KHÁCH (khách đã đăng nhập, không cần link/OTP)
-- ----------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_REVIEW_REQUEST')
BEGIN
    CREATE TABLE CONTRACT_REVIEW_REQUEST (
        contract_review_request_id    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_REVIEW_REQUEST PRIMARY KEY,
        contract_document_id          BIGINT         NOT NULL,
        contract_version_id           BIGINT         NOT NULL,          -- đúng phiên bản được gửi (phải đang khóa)
        contract_review_request_document_hash CHAR(64) NOT NULL,        -- = contract_version_snapshothash lúc gửi
        previous_review_request_id    BIGINT         NULL,              -- lần gửi trước (gửi lại sau khi sửa)
        customer_id                   BIGINT         NOT NULL,          -- tài khoản khách được xem (= CONTRACT_DOCUMENTS.customer_id)
        contract_review_request_message NVARCHAR(1000) NULL,            -- lời nhắn của sale
        sent_by_employee_id           BIGINT         NOT NULL,
        sent_at                       DATETIME2      NOT NULL CONSTRAINT DF_ReviewRequest_SentAt DEFAULT SYSUTCDATETIME(),

        contract_review_request_status VARCHAR(30)   NOT NULL CONSTRAINT DF_ReviewRequest_Status DEFAULT 'SENT',
                                      -- SENT, VIEWED, APPROVED, CHANGES_REQUESTED, REJECTED, REVOKED, SUPERSEDED
        first_viewed_at               DATETIME2      NULL,

        -- Phản hồi của khách
        contract_review_request_customer_note NVARCHAR(4000) NULL,      -- nội dung yêu cầu sửa / lý do từ chối / ghi chú khi đồng ý
        responded_by_customer_id      BIGINT         NULL,
        responded_at                  DATETIME2      NULL,
        responded_ip_address          VARCHAR(45)    NULL,
        responded_user_agent          NVARCHAR(1000) NULL,

        -- Người phụ trách quyết định sau khi khách từ chối
        contract_review_request_followup_decision VARCHAR(20) NULL,     -- NEGOTIATE, CLOSE
        contract_review_request_followup_note NVARCHAR(2000) NULL,
        followup_decided_by_employee_id BIGINT       NULL,
        followup_decided_at           DATETIME2      NULL,

        created_at                    DATETIME2      NOT NULL CONSTRAINT DF_ReviewRequest_CreatedAt DEFAULT SYSUTCDATETIME(),
        updated_at                    DATETIME2      NULL,
        rowversion                    ROWVERSION     NOT NULL,

        CONSTRAINT CK_ReviewRequest_Status CHECK (contract_review_request_status IN
            ('SENT','VIEWED','APPROVED','CHANGES_REQUESTED','REJECTED','REVOKED','SUPERSEDED')),
        CONSTRAINT CK_ReviewRequest_NoteRequired CHECK (
            contract_review_request_status NOT IN ('CHANGES_REQUESTED','REJECTED')
            OR contract_review_request_customer_note IS NOT NULL),
        CONSTRAINT CK_ReviewRequest_Followup CHECK (
            contract_review_request_followup_decision IS NULL
            OR (contract_review_request_followup_decision IN ('NEGOTIATE','CLOSE') AND contract_review_request_status = 'REJECTED')),
        CONSTRAINT FK_ReviewRequest_DocumentVersion FOREIGN KEY (contract_document_id, contract_version_id)
            REFERENCES CONTRACT_VERSION (contract_document_id, contract_version_id),
        CONSTRAINT FK_ReviewRequest_Document FOREIGN KEY (contract_document_id) REFERENCES CONTRACT_DOCUMENTS (contract_document_id),
        CONSTRAINT FK_ReviewRequest_Previous FOREIGN KEY (previous_review_request_id) REFERENCES CONTRACT_REVIEW_REQUEST (contract_review_request_id),
        CONSTRAINT FK_ReviewRequest_Customer FOREIGN KEY (customer_id) REFERENCES CUSTOMER (customer_id),
        CONSTRAINT FK_ReviewRequest_RespondedBy FOREIGN KEY (responded_by_customer_id) REFERENCES CUSTOMER (customer_id),
        CONSTRAINT FK_ReviewRequest_SentBy FOREIGN KEY (sent_by_employee_id) REFERENCES EMPLOYEE (employee_id),
        CONSTRAINT FK_ReviewRequest_FollowupBy FOREIGN KEY (followup_decided_by_employee_id) REFERENCES EMPLOYEE (employee_id)
    );
    CREATE INDEX IX_ReviewRequest_Document ON CONTRACT_REVIEW_REQUEST (contract_document_id, sent_at);
    -- Danh sách "hợp đồng chờ tôi xem" của khách trên cổng 3000
    CREATE INDEX IX_ReviewRequest_Customer_Status ON CONTRACT_REVIEW_REQUEST (customer_id, contract_review_request_status);
    -- Mỗi hồ sơ chỉ có 1 lần gửi đang chờ khách phản hồi
    CREATE UNIQUE INDEX UX_ReviewRequest_Document_Open ON CONTRACT_REVIEW_REQUEST (contract_document_id)
        WHERE contract_review_request_status IN ('SENT','VIEWED');
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- [THÊM-14] KHÓA NGOẠI TỪ BẢNG CÓ SẴN SANG BẢNG MỚI
-- ----------------------------------------------------------------------------------------------------
IF OBJECT_ID('FK_NegotiationComment_ReviewRequest', 'F') IS NULL
    ALTER TABLE CONTRACT_NEGOTIATION_COMMENT ADD CONSTRAINT FK_NegotiationComment_ReviewRequest
        FOREIGN KEY (contract_review_request_id) REFERENCES CONTRACT_REVIEW_REQUEST (contract_review_request_id);
GO
IF OBJECT_ID('FK_NegotiationComment_Customer', 'F') IS NULL
    ALTER TABLE CONTRACT_NEGOTIATION_COMMENT ADD CONSTRAINT FK_NegotiationComment_Customer
        FOREIGN KEY (author_customer_id) REFERENCES CUSTOMER (customer_id);
GO
IF OBJECT_ID('FK_ApprovalRequest_ReviewRequest', 'F') IS NULL
    ALTER TABLE CONTRACT_APPROVAL_REQUEST ADD CONSTRAINT FK_ApprovalRequest_ReviewRequest
        FOREIGN KEY (contract_review_request_id) REFERENCES CONTRACT_REVIEW_REQUEST (contract_review_request_id);
GO
