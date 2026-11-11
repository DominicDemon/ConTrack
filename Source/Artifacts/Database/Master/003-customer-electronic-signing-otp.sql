USE [ConTrackDB];
GO

IF OBJECT_ID(N'dbo.CONTRACT_CUSTOMER_OTP_CHALLENGE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CONTRACT_CUSTOMER_OTP_CHALLENGE
    (
        contract_customer_otp_challenge_id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_CONTRACT_CUSTOMER_OTP_CHALLENGE PRIMARY KEY,
        contract_access_link_id BIGINT NOT NULL,
        contract_electronic_signature_id BIGINT NOT NULL,
        public_challenge_id VARCHAR(64) NOT NULL,
        protected_otp VARCHAR(512) NOT NULL,
        expires_at DATETIME2 NOT NULL,
        failed_attempt_count INT NOT NULL CONSTRAINT DF_CustomerSigningOtp_Attempts DEFAULT (0),
        verified_at DATETIME2 NULL,
        used_at DATETIME2 NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_CustomerSigningOtp_Created DEFAULT SYSUTCDATETIME(),
        rowversion ROWVERSION NOT NULL,
        CONSTRAINT FK_CustomerSigningOtp_AccessLink FOREIGN KEY (contract_access_link_id)
            REFERENCES dbo.CONTRACT_ACCESS_LINK(contract_access_link_id),
        CONSTRAINT FK_CustomerSigningOtp_Signature FOREIGN KEY (contract_electronic_signature_id)
            REFERENCES dbo.CONTRACT_ELECTRONIC_SIGNATURE(contract_electronic_signature_id),
        CONSTRAINT CK_CustomerSigningOtp_Attempts CHECK (failed_attempt_count BETWEEN 0 AND 5),
        CONSTRAINT CK_CustomerSigningOtp_Expiry CHECK (expires_at > created_at),
        CONSTRAINT CK_CustomerSigningOtp_Challenge CHECK (LEN(public_challenge_id) BETWEEN 32 AND 64),
        CONSTRAINT CK_CustomerSigningOtp_ProtectedOtp CHECK (LEN(protected_otp) >= 28)
    );

    CREATE UNIQUE INDEX UX_CustomerSigningOtp_PublicId
        ON dbo.CONTRACT_CUSTOMER_OTP_CHALLENGE(public_challenge_id);
    CREATE INDEX IX_CustomerSigningOtp_Link_Created
        ON dbo.CONTRACT_CUSTOMER_OTP_CHALLENGE(contract_access_link_id, created_at DESC);
END;
GO
