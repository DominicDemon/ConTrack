using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public partial class MasterDbContext
{
    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ContractType> ContractTypes { get; set; }

    public virtual DbSet<OcrBatch> OcrBatches { get; set; }

    public virtual DbSet<OcrJob> OcrJobs { get; set; }

    public virtual DbSet<ContractScanKeyword> ContractScanKeywords { get; set; }

    public virtual DbSet<EmployeeEncryptionKey> EmployeeEncryptionKeys { get; set; }

    public virtual DbSet<FileEncryptionKey> FileEncryptionKeys { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__CONTRACT__F8D66423920D9C63");

            entity.ToTable("CONTRACTS");

            entity.HasIndex(e => e.ContractCode, "UQ__CONTRACT__68CFBDBAD28527ED").IsUnique();

            entity.Property(e => e.ContractId).HasColumnName("contract_id");
            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(200)
                .HasColumnName("approval_status");
            entity.Property(e => e.ContractCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("contract_code");
            entity.Property(e => e.ContractDescription)
                .HasMaxLength(2000)
                .HasColumnName("contract_description");
            entity.Property(e => e.ContractName)
                .HasMaxLength(500)
                .HasColumnName("contract_name");
            entity.Property(e => e.ContractNameen)
                .HasMaxLength(500)
                .HasColumnName("contract_nameen");
            entity.Property(e => e.ContractStatus)
                .HasMaxLength(200)
                .HasColumnName("contract_status");
            entity.Property(e => e.ContractTypeId).HasColumnName("contract_type_id");
            entity.Property(e => e.ContractValue).HasColumnName("contract_value");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_date");
            entity.Property(e => e.CreatedEmployeeId).HasColumnName("created_employee_id");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("currency");
            entity.Property(e => e.DefaultDurationMonths).HasColumnName("default_duration_months");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.EffectiveDate)
                .HasColumnType("datetime")
                .HasColumnName("effective_date");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.ExpirationDate)
                .HasColumnType("datetime")
                .HasColumnName("expiration_date");
            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
            entity.Property(e => e.ModifiedEmployeeId).HasColumnName("modified_employee_id");
            entity.Property(e => e.PartnerId).HasColumnName("partner_id");
            entity.Property(e => e.PaymentTerms).HasColumnName("payment_terms");
            entity.Property(e => e.SignedDate)
                .HasColumnType("datetime")
                .HasColumnName("signed_date");
            entity.Property(e => e.SourceType)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("source_type");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
        });

        modelBuilder.Entity<ContractType>(entity =>
        {
            entity.HasKey(e => e.ContractTypeId).HasName("PK__CONTRACT__B66967DF0B994E87");

            entity.ToTable("CONTRACT_TYPES");

            entity.HasIndex(e => e.ContractTypeCode, "UQ__CONTRACT__E404FEBAA9F05B59").IsUnique();

            entity.Property(e => e.ContractTypeId).HasColumnName("contract_type_id");
            entity.Property(e => e.ContractStatus)
                .HasMaxLength(200)
                .HasColumnName("contract_status");
            entity.Property(e => e.ContractTypeCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("contract_type_code");
            entity.Property(e => e.ContractTypeDescription)
                .HasMaxLength(2000)
                .HasColumnName("contract_type_description");
            entity.Property(e => e.ContractTypeName)
                .HasMaxLength(500)
                .HasColumnName("contract_type_name");
            entity.Property(e => e.ContractTypeNameen)
                .HasMaxLength(500)
                .HasColumnName("contract_type_nameen");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_date");
            entity.Property(e => e.CreatedEmployeeId).HasColumnName("created_employee_id");
            entity.Property(e => e.DefaultDurationMonths).HasColumnName("default_duration_months");
            entity.Property(e => e.ModifiedEmployeeId).HasColumnName("modified_employee_id");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
        });

        modelBuilder.Entity<OcrBatch>(entity =>
        {
            entity.HasKey(e => e.OcrPatchId).HasName("PK__OCR_BATC__3F5512F6ED23FD0F");

            entity.ToTable("OCR_BATCH");

            entity.Property(e => e.OcrPatchId).HasColumnName("ocr_patch_id");
            entity.Property(e => e.BatchStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("batch_status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.TotalFiles).HasColumnName("total_files");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedByEmployeeId).HasColumnName("updated_by_employee_id");
            entity.Property(e => e.UploadedByEmployeeId).HasColumnName("uploaded_by_employee_id");
        });

        modelBuilder.Entity<OcrJob>(entity =>
        {
            entity.HasKey(e => e.OcrJobId).HasName("PK__OCR_JOB__AA5F821BBDB7EE67");

            entity.ToTable("OCR_JOB");

            entity.Property(e => e.OcrJobId).HasColumnName("ocr_job_id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
            entity.Property(e => e.OcrBatchId).HasColumnName("ocr_batch_id");
            entity.Property(e => e.OcrJobAvgConfidenceScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("ocr_job_avg_confidence_score");
            entity.Property(e => e.OcrJobProcessingStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("QUEUED")
                .HasColumnName("ocr_job_processing_status");
            entity.Property(e => e.OcrText).HasColumnName("ocr_text");
            entity.Property(e => e.PageCount).HasColumnName("page_count");
            entity.Property(e => e.ScanQuality)
                .HasMaxLength(100)
                .HasColumnName("scan_quality");
            entity.Property(e => e.ScannedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("scanned_at");
            entity.Property(e => e.ScannedEmployeeId).HasColumnName("scanned_employee_id");
        });

        modelBuilder.Entity<ContractScanKeyword>(entity =>
        {
            entity.ToTable("CONTRACT_SCAN_KEYWORD");

            entity.HasIndex(e => new { e.ContractScanKeywordType, e.ContractScanKeywordValue }, "IX_ContractScanKeyword_Value");

            entity.HasIndex(e => new { e.FileStorageFileid, e.ContractScanKeywordType, e.ContractScanKeywordValue }, "UX_ContractScanKeyword_File_Value").IsUnique();

            entity.Property(e => e.ContractScanKeywordId).HasColumnName("contract_scan_keyword_id");
            entity.Property(e => e.ContractScanKeywordPagenumber).HasColumnName("contract_scan_keyword_pagenumber");
            entity.Property(e => e.ContractScanKeywordSource)
                .HasDefaultValue(1)
                .HasColumnName("contract_scan_keyword_source");
            entity.Property(e => e.ContractScanKeywordType).HasColumnName("contract_scan_keyword_type");
            entity.Property(e => e.ContractScanKeywordValue)
                .HasMaxLength(400)
                .HasColumnName("contract_scan_keyword_value");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByEmployeeId).HasColumnName("created_by_employee_id");
            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
        });

        modelBuilder.Entity<EmployeeEncryptionKey>(entity =>
        {
            entity.ToTable("EMPLOYEE_ENCRYPTION_KEY");

            entity.HasIndex(e => e.EmployeeId, "UX_EmployeeEncryptionKey_ActiveEmployee")
                .IsUnique()
                .HasFilter("([employee_encryption_key_status]='ACTIVE')");

            entity.Property(e => e.EmployeeEncryptionKeyId).HasColumnName("employee_encryption_key_id");
            entity.Property(e => e.EmployeeEncryptionKeyIterations).HasColumnName("employee_encryption_key_iterations");
            entity.Property(e => e.EmployeeEncryptionKeyPrivatekey).HasColumnName("employee_encryption_key_privatekey");
            entity.Property(e => e.EmployeeEncryptionKeyPublickey).HasColumnName("employee_encryption_key_publickey");
            entity.Property(e => e.EmployeeEncryptionKeySalt)
                .HasMaxLength(32)
                .HasColumnName("employee_encryption_key_salt");
            entity.Property(e => e.EmployeeEncryptionKeyStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("employee_encryption_key_status");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issued_at");
            entity.Property(e => e.IssuedByEmployeeId).HasColumnName("issued_by_employee_id");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        });

        modelBuilder.Entity<FileEncryptionKey>(entity =>
        {
            entity.ToTable("FILE_ENCRYPTION_KEY");

            entity.HasIndex(e => e.EmployeeEncryptionKeyId, "IX_FileEncryptionKey_Key");

            entity.HasIndex(e => new { e.FileStorageFileid, e.EmployeeEncryptionKeyId }, "UX_FileEncryptionKey_File_Key")
                .IsUnique()
                .HasFilter("([employee_encryption_key_id] IS NOT NULL)");

            entity.HasIndex(e => e.FileStorageFileid, "UX_FileEncryptionKey_File_Recovery")
                .IsUnique()
                .HasFilter("([employee_encryption_key_id] IS NULL)");

            entity.Property(e => e.FileEncryptionKeyId).HasColumnName("file_encryption_key_id");
            entity.Property(e => e.EmployeeEncryptionKeyId).HasColumnName("employee_encryption_key_id");
            entity.Property(e => e.FileEncryptionKeyRecoverykeyid)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("file_encryption_key_recoverykeyid");
            entity.Property(e => e.FileEncryptionKeyWrappedkey).HasColumnName("file_encryption_key_wrappedkey");
            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedByEmployeeId).HasColumnName("granted_by_employee_id");
        });
    }
}

