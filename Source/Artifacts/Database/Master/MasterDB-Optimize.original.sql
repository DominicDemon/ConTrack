-- BỎ CÁC BẢNG: CONTRACT, CONTRACTAPPENDIX.
-- SỬA ĐỔI TÊN TOÀN BỘ CÁC BẢNG + TRƯỜNG THÔNG TIN THÀNH DƯỚI DẠNG "_".
-- THÊM CÁC BẢNG: CONTRACT_TEMPLATE_FIELDS, CONTRACT_DOCUMENTS, CONTRACT_DOCUMENT_RELATION, CONTRACT_DYNAMIC_VALUES,
--				  CONTRACT_DOCUMENT_FILES, APPROVAL_WORKFLOW_STEP.
-- * CHUYỂN ĐỔI TOÀN BỘ CÁC TÀI LIỆU HỢP ĐỒNG, PHỤ LỤC, BIÊN BẢN BÀN GIAO,... 
-- SANG QUẢN LÝ TỐI ƯU DÙNG CHUNG TRONG CONTRACT_DOCUMENTS (KHÔNG QUẢN LÝ THÀNH TỪNG BẢNG RIÊNG LẺ NỮA).
-- THIẾT KẾ MÔ HÌNH (FIELD_KEY + FIELD_VALUE) CỰC LINH ĐỘNG TRONG CÁCH LƯU TRỮ THÔNG TIN RIÊNG LẺ CỦA TỪNG LOẠI TÀI LIỆU, MẪU.
-- FIELD_KEY + FIELD_VALUE LINH ĐỘNG TRONG VIỆC KHÔNG CẦN PHẢI THÊM CÁC BẢNG HAY TRƯỜNG MỖI KHI LOẠI TÀI LIỆU CẦN THÊM THÔNG TIN MỚI, KHÁC.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CUSTOMER_INTERACTION')
BEGIN
    CREATE TABLE CUSTOMER_INTERACTION (
        customer_interaction_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CUSTOMER_INTERACTION PRIMARY KEY,
        customerid BIGINT NOT NULL,
        employeeid BIGINT NOT NULL,
        customer_interaction_interaction_at DATETIME NOT NULL DEFAULT GETDATE(),
        customer_interaction_interactiontype VARCHAR(50) NOT NULL,
        customer_interaction_interactionsubject NVARCHAR(1000) NOT NULL,
        customer_interaction_interactioncontent NVARCHAR(MAX) NULL,
        customer_interaction_nextfollowupdate DATETIME NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- PRODUCT 
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CATEGORIES')
BEGIN
    CREATE TABLE CATEGORIES (
        category_id INT IDENTITY(1,1) NOT NULL CONSTRAINT CATEGORIES PRIMARY KEY,
        category_name NVARCHAR(500) NOT NULL,
        category_short_desc NVARCHAR(1000) NULL,
        category_desc NVARCHAR(MAX) NULL,
        category_image VARCHAR(50) NULL,
        category_parentid INT NULL,
        category_sortorder INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PRODUCTS')
BEGIN
    CREATE TABLE PRODUCTS (
        product_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PRODUCTS PRIMARY KEY,
        product_code VARCHAR(20) NULL,
        product_name NVARCHAR(500) NOT NULL,
        product_nameen NVARCHAR(500) NULL,
        product_shortname NVARCHAR(100) NULL,
        product_unitname NVARCHAR(100) NULL,
        product_unitnameen NVARCHAR(100) NULL,
        product_slogan NVARCHAR(400) NULL,
        product_shortdesc NVARCHAR(2000) NULL,
        product_largeimage VARCHAR(500) NULL,
        product_smallimage VARCHAR(500) NULL,
        product_iconclass VARCHAR(50) NULL,
        product_document VARCHAR(200) NULL,
        product_tags NVARCHAR(500) NULL,
        product_deployment NVARCHAR(4000) NULL,
        category_id INT NULL,
        product_suggested_price DECIMAL(18,2) NULL,
        product_quantity INT NULL,
        product_sortorder INT NULL,
        product_rewrite VARCHAR(200) NULL,
        product_titlebrowser NVARCHAR(500) NULL,
        product_metadescription NVARCHAR(2000) NULL,
        product_metakeyword NVARCHAR(2000) NULL,
        product_others NVARCHAR(4000) NULL,
        product_created_at DATETIME NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- DỊCH VỤ
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SERVICE_TYPES')
BEGIN
    CREATE TABLE SERVICE_TYPES (
        servicetype_id INT IDENTITY(1,1) NOT NULL CONSTRAINT SERVICE_TYPES PRIMARY KEY,
        servicetype_name NVARCHAR(200) NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SERVICES')
BEGIN
    CREATE TABLE SERVICES (
        service_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT SERVICES PRIMARY KEY,
        service_name NVARCHAR(2000) NOT NULL,
        service_nameen NVARCHAR(500) NULL,
        service_unitname NVARCHAR(100) NULL,
        service_unitnameen NVARCHAR(100) NULL,
        service_price DECIMAL(18,2) NULL,
        servicetype_id INT NULL,
        service_region INT NULL,
        service_imageicon VARCHAR(50) NULL,
        service_fptaccounts bigint NULL,
        service_mysql INT NULL,
        service_haschild BIT NOT NULL CONSTRAINT DF_tbl_Services_HasChild DEFAULT 0,
        service_rewrite VARCHAR(300) NULL,
        service_titlebrowser NVARCHAR(500) NULL,
        service_metadescription NVARCHAR(2000) NULL,
        service_metakeyword NVARCHAR(2000) NULL,
        service_others NVARCHAR(4000) NULL,
        service_status INT NOT NULL CONSTRAINT DF_tbl_Services_Status DEFAULT 0,
        service_datecreated DATETIME NULL CONSTRAINT DF_tbl_Services_DateCreated DEFAULT GETDATE(),
        service_datemodified DATETIME NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- NƠI LƯU ĐỊA CHỈ CÁC LOẠI FILE 
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FILE_STORAGE')
BEGIN
    CREATE TABLE FILE_STORAGE (
        file_storage_fileid BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT FILE_STORAGE PRIMARY KEY,
        file_storage_tenantcode VARCHAR(100) NULL,
		file_storage_objectid BIGINT NULL,
        file_storage_objecttype VARCHAR(50) NOT NULL,
        file_storage_filetype VARCHAR(100) NOT NULL,
        file_storage_filename NVARCHAR(300) NOT NULL,
        file_storage_filepath NVARCHAR(1000) NOT NULL,
        file_storage_storagekey VARCHAR(1000) NOT NULL,
        file_storage_contenttype VARCHAR(255) NOT NULL,
        file_storage_filesize BIGINT NOT NULL,
        file_storage_sha256 CHAR(64) NOT NULL,
        file_storage_uploadedbyemployeeid BIGINT NULL,
		file_storage_uploadedbycustomerid BIGINT NULL,
        file_storage_uploadeddate DATETIME NOT NULL CONSTRAINT DF_FileStorage_UploadedDate DEFAULT SYSUTCDATETIME()
    );
	CREATE INDEX IX_FileStorage_Object ON FILE_STORAGE(file_storage_objecttype, file_storage_objectid);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- MẪU HỢP ĐỒNG 
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_TEMPLATE')
BEGIN
    CREATE TABLE CONTRACT_TEMPLATE (
        contract_template_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_TEMPLATE PRIMARY KEY,
        contract_template_code VARCHAR(50) NOT NULL UNIQUE,
        contract_template_name NVARCHAR(500) NOT NULL,
        contract_template_nameen NVARCHAR(500) NULL,
        contract_template_description NVARCHAR(2000) NULL,
        contract_template_document_type INT NOT NULL, -- 1: Hợp đồng, 2: Phụ lục, 3: Biên bản bàn giao, 4: Biên lai...
        contract_template_language_mode INT NOT NULL DEFAULT 1,
        is_active BIT NOT NULL CONSTRAINT DF_ContractTemplate_IsActive DEFAULT 1,
        created_date DATETIME2 NOT NULL CONSTRAINT DF_ContractTemplate_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_date DATETIME2 NULL,
        created_employee_id INT NULL,
        modified_employee_id INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_TEMPLATE_FIELDS')
BEGIN
    CREATE TABLE CONTRACT_TEMPLATE_FIELDS (
        contract_template_field_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_TEMPLATE_FIELDS PRIMARY KEY,
        contract_template_id BIGINT NOT NULL, 
        contract_template_field_key VARCHAR(100) NOT NULL,
        contract_template_field_label NVARCHAR(255) NOT NULL,
        contract_template_field_data_type VARCHAR(50) NOT NULL DEFAULT 'TEXT',
        is_required BIT NOT NULL DEFAULT 1,
        extra_options NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_TemplateFields_TemplateId ON CONTRACT_TEMPLATE_FIELDS(contract_template_id);
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- HỢP ĐỒNG VÀ CÁC LOẠI TÀI LIỆU KHÁC LIÊN QUAN ĐẾN HỢP ĐỒNG
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DOCUMENTS')
BEGIN
    CREATE TABLE CONTRACT_DOCUMENTS (
        contract_document_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENTS PRIMARY KEY,
		contract_document_current_version_id INT NULL,
        tenant_id VARCHAR(100) NULL,
        customer_id BIGINT NOT NULL,
        employee_id BIGINT NOT NULL,
        contract_template_id BIGINT NOT NULL, 
        contract_document_code VARCHAR(50) NULL,
        contract_document_name NVARCHAR(1000) NOT NULL,
        contract_document_type INT NOT NULL DEFAULT 1, 
        contract_document_status INT NOT NULL CONSTRAINT DF_Doc_Status DEFAULT 0,
        created_date DATETIME2 NOT NULL CONSTRAINT DF_Doc_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_date DATETIME2 NULL,
        row_version ROWVERSION NOT NULL
    );
    CREATE INDEX IX_ContractDocuments_TemplateId ON CONTRACT_DOCUMENTS(contract_template_id);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_VERSION')
BEGIN
    CREATE TABLE CONTRACT_VERSION (
        contract_version_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_VERSION PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_version_no BIGINT NOT NULL,
        contract_template_id BIGINT NOT NULL,
        contract_version_changenote NVARCHAR(2000) NULL,
        contract_version_subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
        contract_version_totaldiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        contract_version_totalvat DECIMAL(18,2) NOT NULL DEFAULT 0,
        contract_version_totalamount DECIMAL(18,2) NOT NULL DEFAULT 0,
        contract_version_currencycode CHAR(3) NOT NULL CONSTRAINT DF_ContractVersion_CurrencyCode DEFAULT 'VND',
        contract_version_islocked BIT NOT NULL CONSTRAINT DF_ContractVersion_IsLocked DEFAULT 0,
        contract_version_lockeddate DATETIME2 NULL,
        contract_version_lockedbyemployeeid INT NULL,
        contract_version_snapshothash CHAR(64) NULL,
		contract_version_signdate DATETIME2 NULL,
		contract_version_effective_date DATETIME2 NULL,
		contract_version_expire_date DATETIME2 NULL,
        contract_version_created_date DATETIME2 NOT NULL CONSTRAINT DF_ContractVersion_CreatedDate DEFAULT SYSUTCDATETIME(),
        contract_version_rowversion ROWVERSION NOT NULL
    );
	CREATE UNIQUE INDEX UX_ContractVersion_Document_VersionNo ON CONTRACT_VERSION (contract_document_id, contract_version_version_no);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DOCUMENT_ITEMS')
BEGIN
    CREATE TABLE CONTRACT_DOCUMENT_ITEMS (
        contract_document_item_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENT_ITEMS PRIMARY KEY,
        contract_document_id BIGINT NOT NULL, 
		contract_version_id BIGINT NOT NULL,
        product_id BIGINT NULL,  
        contract_document_item_code VARCHAR(100) NULL,
        contract_document_item_name NVARCHAR(500) NOT NULL,
        contract_document_item_quantity DECIMAL(18,4) NOT NULL DEFAULT 1,
        contract_document_item_unit_price DECIMAL(18,2) NOT NULL,
        contract_document_item_line_subtotal DECIMAL(18,2) NOT NULL,
        contract_document_item_discount_percent DECIMAL(5,2) NOT NULL DEFAULT 0,
        contract_document_item_fixed_discount_amount DECIMAL(18,2) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_DocumentItems_DocumentId ON CONTRACT_DOCUMENT_ITEMS(contract_document_id);
    CREATE INDEX IX_DocumentItems_ProductId ON CONTRACT_DOCUMENT_ITEMS(product_id);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DOCUMENT_RELATION')
BEGIN
    CREATE TABLE CONTRACT_DOCUMENT_RELATION (
        contract_document_relation_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENT_RELATION PRIMARY KEY,
        parent_document_id BIGINT NOT NULL,
        child_document_id BIGINT NOT NULL,
        contract_document_relation_type VARCHAR(50) NOT NULL, 
        created_date DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    ALTER TABLE CONTRACT_DOCUMENT_RELATION ADD CONSTRAINT UQ_Parent_Child UNIQUE(parent_document_id, child_document_id);
	ALTER TABLE CONTRACT_DOCUMENT_RELATION ADD CONSTRAINT CK_ContractDocumentRelation_NotSelf CHECK (parent_document_id <> child_document_id);
END;
GO


IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DYNAMIC_VALUES')
BEGIN
    CREATE TABLE CONTRACT_DYNAMIC_VALUES (
        contract_dynamic_value_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DYNAMIC_VALUES PRIMARY KEY,
        contract_document_id BIGINT NOT NULL, 
		contract_version_id BIGINT NOT NULL,
        field_key VARCHAR(100) NOT NULL,
        field_value NVARCHAR(MAX) NOT NULL
    );
    CREATE INDEX IX_Dynamic_Values_Key_Doc ON CONTRACT_DYNAMIC_VALUES(field_key, contract_document_id);
    CREATE INDEX IX_Dynamic_Values_DocumentId ON CONTRACT_DYNAMIC_VALUES(contract_document_id);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_TERM')
BEGIN
    CREATE TABLE CONTRACT_TERM (
        contract_term_termid BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_TERM PRIMARY KEY,
        contract_document_id BIGINT NOT NULL, 
		contract_version_id BIGINT NOT NULL,
        contract_term_sourcetemplatetermid BIGINT NULL,
        contract_term_termcode VARCHAR(100) NOT NULL,
        contract_term_termtitle NVARCHAR(500) NOT NULL,
        contract_term_termtitleen NVARCHAR(500) NULL,
        contract_term_termcontent NVARCHAR(MAX) NULL,
        contract_term_termcontenten NVARCHAR(MAX) NULL,
        contract_term_isnegotiable BIT NOT NULL CONSTRAINT DF_ContractTerm_IsNegotiable DEFAULT 0,
        contract_term_termkind INT NOT NULL CONSTRAINT DF_ContractTerm_TermKind DEFAULT 0,
        contract_term_bilingualcontentmode INT NOT NULL DEFAULT 1,
        contract_term_displayorder BIGINT NOT NULL CONSTRAINT DF_ContractTerm_DisplayOrder DEFAULT 0,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_ContractTerm_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_DOCUMENT_FILES')
BEGIN
    CREATE TABLE CONTRACT_DOCUMENT_FILES (
		contract_document_file_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CONTRACT_DOCUMENT_FILES PRIMARY KEY,
		contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NULL,
        file_storage_fileid BIGINT NOT NULL,
        contract_document_file_role INT NOT NULL,
        is_primary BIT NOT NULL CONSTRAINT DF_ContractDocumentFiles_IsPrimary DEFAULT 0,
		created_date DATETIME2 NOT NULL CONSTRAINT DF_ContractDocumentFiles_CreatedDate DEFAULT SYSUTCDATETIME(),
		created_employee_id BIGINT NULL,
        CONSTRAINT CK_ContractDocumentFiles_FileRole CHECK (contract_document_file_role BETWEEN 1 AND 5)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_PAYMENT_MILESTONE')
BEGIN
    CREATE TABLE CONTRACT_PAYMENT_MILESTONE (
        contract_payment_milestone_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_PAYMENT_MILESTONE PRIMARY KEY,
        contract_document_id BIGINT NOT NULL, 
        contract_version_id BIGINT NOT NULL,
        contract_term_id BIGINT NOT NULL,
		invoice_id BIGINT NULL,
        contract_payment_milestone_code VARCHAR(100) NOT NULL,
        contract_payment_milestone_titlevi NVARCHAR(500) NOT NULL,
        contract_payment_milestone_titleen NVARCHAR(500) NULL,
        contract_payment_milestone_paymentpercent DECIMAL(9,4) NOT NULL,
        contract_payment_milestone_amount DECIMAL(18,2) NOT NULL,
        contract_payment_milestone_dueoffsetdays INT NOT NULL DEFAULT 0,
        contract_payment_milestone_dueanchor INT NOT NULL DEFAULT 1,
        contract_payment_milestone_daycountmode INT NOT NULL DEFAULT 1,
        contract_payment_milestone_anchordate DATE NULL,
        contract_payment_milestone_duedate DATE NULL,
        contract_payment_milestone_paymentstatus INT NOT NULL CONSTRAINT DF_ContractPaymentMilestone_Status DEFAULT 0,
        contract_payment_milestone_paidat DATETIME2 NULL,
        contract_payment_milestone_paidbyemployeeid INT NULL,
        contract_payment_milestone_conditionvi NVARCHAR(2000) NULL,
        contract_payment_milestone_conditionen NVARCHAR(2000) NULL,
        contract_payment_milestone_displayorder BIGINT NOT NULL DEFAULT 0,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_ContractPaymentMilestone_CreatedDate DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO


IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_SIGNED_EVIDENCE')
BEGIN
    CREATE TABLE CONTRACT_SIGNED_EVIDENCE (
        contract_signed_evidence_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_SIGNED_EVIDENCE PRIMARY KEY,
        contract_document_file_id BIGINT NOT NULL,
        contract_signed_evidence_status INT NOT NULL DEFAULT 1,
        contract_signed_evidence_signdate DATE NOT NULL,
        uploaded_by_employeeid BIGINT NOT NULL,
        uploaded_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        superseded_by_employee_id BIGINT NULL,
        superseded_at DATETIME2 NULL,
        supersedes_evidence_id BIGINT NULL,
        contract_signed_evidence_supersedereason NVARCHAR(1000) NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO

-- ----------------------------------------------------------------------------------------------------
-- PHÊ DUYỆT NỘI BỘ, ĐÀM PHÁN & CỔNG KHÁCH HÀNG OTP (CUSTOMER PORTAL)
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'APPROVAL_WORKFLOW')
BEGIN
    CREATE TABLE APPROVAL_WORKFLOW (
        approval_workflow_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT APPROVAL_WORKFLOW PRIMARY KEY,
        approval_workflow_code VARCHAR(50) NOT NULL UNIQUE,
        approval_workflow_name NVARCHAR(200) NOT NULL,
        approval_workflow_description NVARCHAR(1000) NULL,
        approval_workflow_objecttype VARCHAR(50) NOT NULL,
        approval_workflow_isactive BIT NOT NULL DEFAULT 1,
        approval_workflow_createdat DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        approval_workflow_createdbyemployeeid BIGINT NULL,
        approval_workflow_updatedat DATETIME2 NULL,
        approval_workflow_updatedbyemployeeid BIGINT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'APPROVAL_WORKFLOW_STEP')
BEGIN
    CREATE TABLE APPROVAL_WORKFLOW_STEP (
        approval_workflow_step_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT APPROVAL_WORKFLOW_STEP PRIMARY KEY,
        approval_workflow_id BIGINT NOT NULL,
        approval_workflow_step_order BIGINT NOT NULL,
        approval_workflow_step_name NVARCHAR(200) NOT NULL,
        system_role_id BIGINT NOT NULL,
        approval_workflow_step_isrequired BIT NOT NULL DEFAULT 1,
        CONSTRAINT UQ_ApprovalWorkflowStep_Order UNIQUE (approval_workflow_id, approval_workflow_step_order),
        CONSTRAINT CK_ApprovalWorkflowStep_Order CHECK (approval_workflow_step_order > 0)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_APPROVAL_REQUEST')
BEGIN
    CREATE TABLE CONTRACT_APPROVAL_REQUEST (
        contract_approval_request_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_APPROVAL_REQUEST PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        approval_workflow_id BIGINT NOT NULL,
        submitted_by_employee_id BIGINT NOT NULL,
        submitted_at DATETIME2 NOT NULL CONSTRAINT DF_ContractApprovalRequest_SubmittedDate DEFAULT SYSUTCDATETIME(),
        contract_approval_request_status INT NOT NULL CONSTRAINT DF_ContractApprovalRequest_Status DEFAULT 0,
        contract_approval_request_resolved_at DATETIME2 NULL,
        resolved_by_employee_id BIGINT NULL,
        contract_approval_request_decisioncomment NVARCHAR(1000) NULL,
        contract_approval_request_rowversion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_PHONE_VERIFY')
BEGIN
    CREATE TABLE CONTRACT_PHONE_VERIFY (
        contract_phone_verify_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_PHONE_VERIFY PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_phone_verify_phone_source VARCHAR(32) NOT NULL,
        contract_phone_verify_phone_number_normalized VARCHAR(32) NOT NULL,
        contract_phone_verify_reason NVARCHAR(1000) NOT NULL,
        created_by_employeeid BIGINT NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        rowversion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_ACCESS_LINK')
BEGIN
    CREATE TABLE CONTRACT_ACCESS_LINK (
        contract_access_link_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_ACCESS_LINK PRIMARY KEY,
        tenant_id BIGINT NULL,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        contract_access_link_verification_phone_id BIGINT NOT NULL,
        contract_access_link_tokenhash VARCHAR(64) NOT NULL,
        contract_access_link_revocation_reason NVARCHAR(1000) NULL,
        created_by_employee_id BIGINT NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        activated_at DATETIME2 NULL,
        expires_at DATETIME2 NOT NULL,
        revoked_at DATETIME2 NULL,
        revoked_by_employee_id BIGINT NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO

   

-- ----------------------------------------------------------------------------------------------------
-- CHỮ KÝ ĐIỆN TỬ TRỰC TUYẾN (E-SIGNATURE PORTAL: VẼ TAY, TỌA ĐỘ TRANG PDF & XÁC THỰC OTP)
-- Mô hình DocuSign / Base Sign / FPT eContract: Không dùng USB Token / PKI
-- ----------------------------------------------------------------------------------------------------

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_ELECTRONIC_SIGNATURE')
BEGIN
    CREATE TABLE CONTRACT_ELECTRONIC_SIGNATURE (
        contract_electronic_signature_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_ELECTRONIC_SIGNATURE PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,                        -- Hợp đồng ký
        contract_version_id BIGINT NOT NULL,                         -- Phiên bản hợp đồng ký
        contract_electronic_partytype INT NOT NULL,                     -- 1 = Bên A (Doanh nghiệp nội bộ), 2 = Bên B (Khách hàng/Đối tác)
        contract_electronic_signertype VARCHAR(30) NOT NULL,                -- 'InternalEmployee', 'ExternalCustomer'
        employee_id BIGINT NULL,                            -- Nếu Bên A ký: liên kết nhân viên nội bộ
        contract_electronic_signername NVARCHAR(200) NOT NULL,              -- Họ và tên người ký
        contract_electronic_signertitle NVARCHAR(100) NULL,                 -- Chức vụ / Đại diện
        contract_electronic_signerphone VARCHAR(30) NOT NULL,               -- Số điện thoại nhận OTP
        contract_electronic_signeremail VARCHAR(200) NULL,                  -- Email nhận thông báo
        contract_electronic_signaturetype VARCHAR(30) NOT NULL CONSTRAINT DF_CES_Type DEFAULT 'DRAWN', -- 'DRAWN' (vẽ tay canvas), 'TYPED' (gõ tên), 'UPLOADED' (ảnh chữ ký), 'OTP_ACK' (xác nhận OTP)
        contract_electronic_signatureimagestoragekey NVARCHAR(500) NULL,    -- Đường dẫn file ảnh chữ ký nền trong suốt (PNG)
        contract_electronic_customerotpchallengeid INT NULL,                        -- FK sang CONTRACTCUSTOMEROTPCHALLENGE (Bằng chứng OTP đã xác thực)
        contract_electronic_status INT NOT NULL CONSTRAINT DF_CES_Status DEFAULT 0, -- 0 = Chờ ký (Pending), 1 = Đã ký thành công (Signed), 2 = Từ chối ký (Declined)
        contract_electronic_signedat DATETIME2 NULL,                        -- Thời điểm bấm xác nhận ký
        contract_electronic_ipaddress VARCHAR(45) NULL,                     -- Địa chỉ IP người ký (Audit trail pháp lý)
        contract_electronic_useragent NVARCHAR(1000) NULL,                  -- Trình duyệt & Thiết bị người ký
        contract_electronic_document_hash_at_signing VARCHAR(64) NULL,         -- Mã băm SHA-256 nội dung hợp đồng lúc ký (chống sửa đổi sau ký)
        contract_electronic_pagenumber INT NULL,                            -- Trang đóng dấu chữ ký trên file PDF
        contract_electronic_positionx DECIMAL(9,2) NULL,                    -- Tọa độ X trên trang PDF
        contract_electronic_positiony DECIMAL(9,2) NULL,                    -- Tọa độ Y trên trang PDF
        contract_electronic_width DECIMAL(9,2) NULL,                        -- Kích thước khung ký
        contract_electronic_height DECIMAL(9,2) NULL,
        contract_electronic_reject_reason NVARCHAR(1000) NULL,               -- Lý do từ chối nếu không đồng ý ký
        created_at DATETIME2 NOT NULL CONSTRAINT DF_CES_CreatedDate DEFAULT SYSUTCDATETIME(),
        rowversion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_NEGOTIATION_COMMENT')
BEGIN
    CREATE TABLE CONTRACT_NEGOTIATION_COMMENT (
        contract_negotiation_comment_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_NEGOTIATION_COMMENT PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        contract_negotiation_comment_termid INT NULL,
        contract_negotiation_comment_parentcommentid INT NULL,
        contract_negotiation_comment_carriedforwardfromcommentid INT NULL,
        contract_negotiation_comment_carriedforwardfromversionid INT NULL,
        contract_negotiation_comment_source VARCHAR(32) NOT NULL DEFAULT 'ExternalFeedback',
        contract_negotiation_comment_content NVARCHAR(4000) NOT NULL,
        recorded_by_employee_id BIGINT NULL,
        customer_access_session_id BIGINT NULL,
        contract_negotiation_comment_state INT NOT NULL DEFAULT 0,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CONTRACT_HANDOVER_RECORD')
BEGIN
    CREATE TABLE CONTRACT_HANDOVER_RECORD (
        contract_handover_record_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT CONTRACT_HANDOVER_RECORD PRIMARY KEY,
        contract_document_id BIGINT NOT NULL,
        contract_version_id BIGINT NOT NULL,
        contract_template_id BIGINT NOT NULL,
        contract_handover_record_handovercode NVARCHAR(100) NOT NULL,
        contract_handover_record_handover_at DATE NOT NULL,
        contract_handover_record_contractsigndate DATE NOT NULL,
        contract_handover_record_location NVARCHAR(500) NOT NULL,
        contract_handover_record_locationen NVARCHAR(500) NULL,
        contract_handover_record_status INT NOT NULL DEFAULT 0,
        contract_handover_record_snapshothash VARCHAR(64) NULL,
        created_employee_id BIGINT NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_employee_id BIGINT NULL,
        updated_at DATETIME2 NULL,
        finalized_by_employee_id BIGINT NULL,
        finalized_at DATETIME2 NULL,
        signed_by_employee_id BIGINT NULL,
        signed_at DATETIME2 NULL,
        cancelled_by_employee_id BIGINT NULL,
        cancelled_at DATETIME2 NULL,
        contract_handover_record_cancellationreason NVARCHAR(1000) NULL,
        rowversion ROWVERSION NOT NULL
    );
END;
GO

    

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NOTIFICATION')
BEGIN
    CREATE TABLE NOTIFICATION (
        notification_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT NOTIFICATION PRIMARY KEY,
        notification_eventkey VARCHAR(200) NOT NULL,
        notification_modulecode VARCHAR(50) NOT NULL,
        notification_eventcode VARCHAR(80) NOT NULL,
        notification_subjecttype VARCHAR(50) NOT NULL,
        notification_subjectid BIGINT NOT NULL,
        notification_title NVARCHAR(300) NOT NULL,
        notification_body NVARCHAR(1000) NOT NULL,
        notification_actionkey VARCHAR(80) NULL,
        employee_id BIGINT NULL,
        notification_createdatutc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NOTIFICATION_RECIPIENT')
BEGIN
    CREATE TABLE NOTIFICATION_RECIPIENT (
        notification_recipient_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT NOTIFICATION_RECIPIENT PRIMARY KEY,
        notification_id BIGINT NOT NULL,
        employee_id BIGINT NOT NULL,
        notification_recipient_createdatutc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        notification_recipient_readatutc DATETIME2 NULL,
        notification_recipient_dismissedatutc DATETIME2 NULL
    );
END;
GO 

-- ----------------------------------------------------------------------------------------------------
-- MERGE penta master db
-- ----------------------------------------------------------------------------------------------------

CREATE TABLE [CUSTOMER] (
  [customer_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [customer_email] varchar(255),
  [customer_personal_phone] varchar(20) UNIQUE NOT NULL,
  [customer_password_hash] varchar(255) NOT NULL,
  [customer_display_name] nvarchar(200) UNIQUE NOT NULL,
  [customer_personal_tax_identifier] varchar(20),
  [customer_status] int NOT NULL DEFAULT (1),
  [customer_dob] date,
  [customer_gender] varchar(10),
  [customer_address] nvarchar(1000),
  [customer_firstname] nvarchar(100),
  [customer_middlename] nvarchar(100),
  [customer_lastname] nvarchar(100),
  [customer_email_verified_at] datetime2,
  [customer_personal_phone_verified_at] datetime2,
  [customer_last_login_at] datetime2,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [CUSTOMER_BUSINESS] (
  [customer_business_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [customer_id] bigint NOT NULL,
  [customer_business_legal_name] nvarchar(1000) NOT NULL,
  [customer_business_company_tax_identifier] varchar(20) NOT NULL,
  [customer_business_phone] varchar(20),
  [customer_business_email] varchar(100),
  [customer_business_office_address] nvarchar(1000),
  [customer_business_head_office_address] nvarchar(1000),
  [customer_business_type] varchar(100),
  [customer_business_representative_name] nvarchar(500),
  [customer_business_operation_start_date] date,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [DEPARTMENT] (
  [department_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [department_code] varchar(30) UNIQUE NOT NULL,
  [department_name] nvarchar(500) UNIQUE NOT NULL,
  [department_description] nvarchar(2000),
  [department_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [EMPLOYEE] (
  [employee_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [employee_code] varchar(30) UNIQUE NOT NULL,
  [employee_email] varchar(100) UNIQUE,
  [employee_phone] varchar(20) UNIQUE,
  [employee_password_hash] varchar(255) NOT NULL,
  [employee_display_name] nvarchar(200) UNIQUE NOT NULL,
  [employee_username] varchar(100) NOT NULL,
  [employee_personal_identifier] varchar(20) UNIQUE,
  [employee_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [employee_dob] date,
  [employee_gender] varchar(10),
  [employee_address] nvarchar(1000),
  [employee_first_name] nvarchar(100),
  [employee_middle_name] nvarchar(100),
  [employee_last_name] nvarchar(100),
  [employee_email_verified_at] datetime2,
  [employee_hand_phone_verified_at] datetime2,
  [employee_last_login_at] datetime2,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [EMPLOYEE_DEPARTMENT] (
  [employee_id] bigint NOT NULL,
  [department_id] bigint NOT NULL,
  [created_at] datetime2 NOT NULL,
  [created_by] bigint
)
GO

CREATE TABLE [SYSTEM_PERMISSION_GROUP] (
  [system_permission_group_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [system_permission_group_code] varchar(50) UNIQUE NOT NULL,
  [system_permission_group_name] nvarchar(300) NOT NULL,
  [system_permission_group_description] nvarchar(2000),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SYSTEM_PERMISSION] (
  [system_permission_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [system_permission_group_id] bigint NOT NULL,
  [system_permission_code] varchar(100) UNIQUE NOT NULL,
  [system_permission_name] nvarchar(300) NOT NULL,
  [system_permission_resource] varchar(100),
  [system_permission_action] varchar(50),
  [system_permission_description] nvarchar(2000),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SYSTEM_PERMISSION_DEPENDENCY] (
  [system_permission_dependency_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [system_permission_id] bigint NOT NULL,
  [depends_on_system_permission_id] bigint NOT NULL,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint
)
GO

CREATE TABLE [SYSTEM_ROLE] (
  [system_role_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [system_role_code] varchar(50) UNIQUE NOT NULL,
  [system_role_name] nvarchar(200) NOT NULL,
  [system_role_scope] varchar(20) NOT NULL DEFAULT 'SYSTEM',
  [system_role_description] nvarchar(500),
  [system_role_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SYSTEM_ROLE_PERMISSION] (
  [system_role_id] bigint NOT NULL,
  [system_permission_id] bigint NOT NULL,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  PRIMARY KEY ([system_role_id], [system_permission_id])
)
GO

CREATE TABLE [EMPLOYEE_ROLE] (
  [employee_role_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [employee_id] bigint NOT NULL,
  [system_role_id] bigint NOT NULL,
  [system_member_role_assigned_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2
)
GO

CREATE TABLE [SAAS_TYPE] (
  [saas_type_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [product_id] bigint NOT NULL,
  [saas_type_code] varchar(50) UNIQUE NOT NULL,
  [saas_type_name] nvarchar(1000) NOT NULL,
  [saas_type_description] nvarchar(2000),
  [saas_type_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SAAS_PERMISSION_GROUP] (
  [permission_group_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [saas_type_id] bigint NOT NULL,
  [group_code] varchar(50) NOT NULL,
  [group_name] nvarchar(300) NOT NULL,
  [group_description] nvarchar(1000),
  [group_icon] varchar(100),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SAAS_PERMISSION] (
  [permission_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [permission_group_id] bigint NOT NULL,
  [permission_code] varchar(100) UNIQUE NOT NULL,
  [permission_name] nvarchar(300) NOT NULL,
  [permission_description] nvarchar(1000),
  [permission_point] int NOT NULL DEFAULT (0),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [SAAS_PERMISSION_DEPENDENCY] (
  [dependency_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [permission_id] bigint NOT NULL,
  [depends_on_permission_id] bigint NOT NULL,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint
)
GO

CREATE TABLE [PACKAGE_LEVEL] (
  [package_level_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [saas_type_id] bigint NOT NULL,
  [package_level_code] varchar(50) NOT NULL,
  [package_level_name] nvarchar(500) NOT NULL,
  [package_level_description] nvarchar(1000),
  [package_level_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [PACKAGE_LEVEL_PERMISSION] (
  [package_level_id] bigint NOT NULL,
  [permission_id] bigint NOT NULL,
  PRIMARY KEY ([package_level_id], [permission_id])
)
GO

CREATE TABLE [PACKAGE_LEVEL_CONVERSION_RATE] (
  [package_level_conversion_rate_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [saas_type_id] bigint NOT NULL,
  [from_level_id] bigint NOT NULL,
  [to_level_id] bigint NOT NULL,
  [conversion_rate] decimal(18,6) NOT NULL
)
GO

CREATE TABLE [PLAN] (
  [plan_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [saas_type_id] bigint NOT NULL,
  [package_level_id] bigint NOT NULL,
  [plan_code] varchar(50) NOT NULL,
  [plan_name] nvarchar(150) NOT NULL,
  [current_active_version_id] bigint,
  [plan_status] varchar(20) NOT NULL DEFAULT 'DRAFT',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [PLAN_VERSION] (
  [plan_version_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [plan_id] bigint NOT NULL,
  [plan_version_number] int NOT NULL,
  [plan_version_duration_days] int NOT NULL,
  [plan_version_price] decimal(19,4) NOT NULL,
  [plan_version_currency] varchar(20) NOT NULL DEFAULT 'VND',
  [plan_version_effective_from] datetime2 NOT NULL,
  [plan_version_effective_to] datetime2,
  [plan_version_status] varchar(20) NOT NULL DEFAULT 'DRAFT',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint
)
GO

CREATE TABLE [TENANT] (
  [tenant_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [customer_id] bigint,
  [saas_type_id] bigint NOT NULL,
  [package_level_id] bigint NOT NULL,
  [tenant_code] varchar(50) UNIQUE NOT NULL,
  [tenant_name] nvarchar(500) NOT NULL,
  [tenant_subdomain] varchar(100) UNIQUE NOT NULL,
  [tenant_status] varchar(20) NOT NULL DEFAULT 'PENDING',
  [tenant_expired_at] datetime2,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2
)
GO

CREATE TABLE [TENANT_DATABASE] (
  [tenant_database_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [tenant_id] bigint UNIQUE NOT NULL,
  [tenant_database_provider] varchar(50) NOT NULL,
  [tenant_database_host] varchar(255) NOT NULL,
  [tenant_database_port] int NOT NULL,
  [tenant_database_db_name] varchar(255) NOT NULL,
  [tenant_database_user] varchar(255) NOT NULL,
  [tenant_database_password_encrypted] varchar(1000),
  [tenant_database_provisioning_status] varchar(30) NOT NULL DEFAULT 'FRESH',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2
)
GO

CREATE TABLE [PROMOTION] (
  [promotion_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [promotion_code] varchar(50) UNIQUE NOT NULL,
  [promotion_name] nvarchar(500) NOT NULL,
  [promotion_description] nvarchar(1000),
  [promotion_discount_type] varchar(20) NOT NULL,
  [promotion_discount_value] decimal(19,4) NOT NULL,
  [promotion_max_discount_amount] decimal(19,4),
  [is_stackable] bit NOT NULL DEFAULT (0),
  [stacking_priority] int NOT NULL DEFAULT (0),
  [max_usage_per_customer] int NOT NULL DEFAULT (1),
  [promotion_usage_limit] int,
  [promotion_usage_count] int NOT NULL DEFAULT (0),
  [promotion_start_at] datetime2 NOT NULL,
  [promotion_end_at] datetime2 NOT NULL,
  [promotion_status] varchar(20) NOT NULL DEFAULT 'ACTIVE',
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [PROMOTION_SAAS_TYPE] (
  [promotion_id] bigint NOT NULL,
  [saas_type_id] bigint NOT NULL,
  PRIMARY KEY ([promotion_id], [saas_type_id])
)
GO

CREATE TABLE [PROMOTION_PLAN] (
  [promotion_id] bigint NOT NULL,
  [plan_id] bigint NOT NULL,
  PRIMARY KEY ([promotion_id], [plan_id])
)
GO

CREATE TABLE [PROMOTION_CUSTOMER_USAGE] (
  [customer_usage_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [promotion_id] bigint NOT NULL,
  [customer_id] bigint NOT NULL,
  [times_used] int NOT NULL DEFAULT (0),
  [last_used_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [INVOICE] (
  [invoice_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [invoice_code] varchar(100) UNIQUE NOT NULL,
  [contract_version_id] bigint null,
  [tenant_id] bigint NOT NULL,
  [customer_id] bigint NOT NULL,
  [customer_business_id] bigint,
  [plan_version_id] bigint NOT NULL,
  [subscription_days] int NOT NULL,
  [is_tax_invoice_requested] bit NOT NULL DEFAULT (0),
  [invoice_buyer_type] varchar(20) NOT NULL DEFAULT 'INDIVIDUAL',
  [invoice_billing_name] nvarchar(500) NOT NULL,
  [invoice_billing_tax_identifier] varchar(50),
  [invoice_billing_address] nvarchar(1000),
  [invoice_billing_email] varchar(255),
  [invoice_billing_phone] varchar(30),
  [invoice_subtotal] decimal(19,4) NOT NULL,
  [invoice_discount_amount] decimal(19,4) NOT NULL DEFAULT (0),
  [invoice_total_amount] decimal(19,4) NOT NULL,
  [invoice_currency] varchar(10) NOT NULL DEFAULT 'VND',
  [invoice_status] varchar(30) NOT NULL DEFAULT 'DRAFT',
  [invoice_issued_at] datetime2,
  [invoice_paid_at] datetime2,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint,
  [updated_at] datetime2,
  [updated_by] bigint
)
GO

CREATE TABLE [INVOICE_PROMOTION_DETAIL] (
  [invoice_promotion_detail_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [invoice_id] bigint NOT NULL,
  [promotion_id] bigint NOT NULL,
  [discount_value_applied] decimal(19,4) NOT NULL,
  [actual_money_deducted] decimal(19,4) NOT NULL,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [PAYMENT] (
  [payment_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [invoice_id] bigint UNIQUE NOT NULL,
  [payment_reference] varchar(100) UNIQUE NOT NULL,
  [payment_method] varchar(30) NOT NULL,
  [payment_provider] varchar(100),
  [payment_provider_transaction_id] varchar(255),
  [payment_amount] decimal(19,4) NOT NULL,
  [payment_currency] varchar(20) NOT NULL DEFAULT 'VND',
  [payment_status] varchar(20) NOT NULL DEFAULT 'SUCCESS',
  [payment_paid_at] datetime2,
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [created_by] bigint
)
GO

CREATE TABLE [AUDIT_LOG] (
  [audit_log_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [action_type] varchar(50) NOT NULL,
  [entity_name] varchar(100) NOT NULL,
  [entity_id] varchar(100) NOT NULL,
  [actor_type] varchar(20) NOT NULL,
  [actor_id] bigint,
  [actor_ip_address] varchar(45),
  [old_data] nvarchar(max),
  [new_data] nvarchar(max),
  [created_at] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [TICKET_SUPPORT] (
  [ticket_support_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [customer_id] bigint,
  [tenant_id] bigint,
  [product_id] bigint null,
  [service_id] bigint null,
  [ticket_code] varchar(30),
  [ticket_title] nvarchar(1000),
  [ticket_description] nvarchar(max),
  [ticket_priority] varchar(20),
  [ticket_status] varchar(30),
  [ticket_resolution] nvarchar(max),
  [ticket_resolved_at] datetime,
  [ticket_closed_at] datetime,
  [created_at] datetime NOT NULL,
  [updated_at] datetime
)
GO

CREATE TABLE [TICKET_MESSAGE] (
  [ticket_message_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [ticket_support_id] bigint,
  [ticket_message_sender_customer_id] bigint,
  [ticket_message_sender_employee_id] bigint,
  [ticket_message_content] nvarchar(max),
  [ticket_message_type] varchar(20),
  [created_at] datetime
)
GO

CREATE TABLE [TICKET_SUPPORT_EMPLOYEE] (
  [ticket_support_employee_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [ticket_support_id] bigint,
  [employee_id] bigint,
  [ticket_support_employee_assignment_role] varchar(20),
  [ticket_support_employee_assigned_at] datetime,
  [ticket_support_employee_unassigned_at] datetime,
  [ticket_support_employee_assigned_by_employee_id] bigint
)
GO

CREATE UNIQUE INDEX [UQ_Customer_Email_NotNull] ON [CUSTOMER] ("customer_email")
GO

CREATE UNIQUE INDEX [EMPLOYEE_DEPARTMENT_index_1] ON [EMPLOYEE_DEPARTMENT] ("employee_id", "department_id")
GO

CREATE UNIQUE INDEX [SYSTEM_PERMISSION_DEPENDENCY_index_2] ON [SYSTEM_PERMISSION_DEPENDENCY] ("system_permission_id", "depends_on_system_permission_id")
GO

CREATE UNIQUE INDEX [EMPLOYEE_ROLE_index_3] ON [EMPLOYEE_ROLE] ("employee_id", "system_role_id")
GO

CREATE UNIQUE INDEX [SAAS_PERMISSION_GROUP_index_4] ON [SAAS_PERMISSION_GROUP] ("saas_type_id", "group_code")
GO

CREATE UNIQUE INDEX [SAAS_PERMISSION_DEPENDENCY_index_5] ON [SAAS_PERMISSION_DEPENDENCY] ("permission_id", "depends_on_permission_id")
GO

CREATE UNIQUE INDEX [PACKAGE_LEVEL_index_6] ON [PACKAGE_LEVEL] ("saas_type_id", "package_level_code")
GO

CREATE UNIQUE INDEX [PACKAGE_LEVEL_CONVERSION_RATE_index_7] ON [PACKAGE_LEVEL_CONVERSION_RATE] ("saas_type_id", "from_level_id", "to_level_id")
GO

CREATE UNIQUE INDEX [PLAN_index_8] ON [PLAN] ("saas_type_id", "plan_code")
GO

CREATE UNIQUE INDEX [PLAN_VERSION_index_9] ON [PLAN_VERSION] ("plan_id", "plan_version_number")
GO

CREATE UNIQUE INDEX [PROMOTION_CUSTOMER_USAGE_index_10] ON [PROMOTION_CUSTOMER_USAGE] ("promotion_id", "customer_id")
GO

CREATE INDEX [IX_AuditLog_Entity] ON [AUDIT_LOG] ("entity_name", "entity_id")
GO

CREATE INDEX [IX_AuditLog_Actor] ON [AUDIT_LOG] ("actor_type", "actor_id")
GO