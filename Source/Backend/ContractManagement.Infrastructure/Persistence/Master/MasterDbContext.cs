using System;
using System.Collections.Generic;
using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public partial class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ApprovalWorkflow> ApprovalWorkflows { get; set; }

    public virtual DbSet<ApprovalWorkflowStep> ApprovalWorkflowSteps { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ContractAccessLink> ContractAccessLinks { get; set; }

    public virtual DbSet<ContractApprovalRequest> ContractApprovalRequests { get; set; }

    public virtual DbSet<ContractReviewRequest> ContractReviewRequests { get; set; }

    public virtual DbSet<SmsOutbox> SmsOutboxes { get; set; }

    public virtual DbSet<SmsProviderCallback> SmsProviderCallbacks { get; set; }

    public virtual DbSet<ContractDocument> ContractDocuments { get; set; }

    public virtual DbSet<ContractDocumentFile> ContractDocumentFiles { get; set; }

    public virtual DbSet<ContractDocumentItem> ContractDocumentItems { get; set; }

    public virtual DbSet<ContractDocumentRelation> ContractDocumentRelations { get; set; }

    public virtual DbSet<ContractDynamicValue> ContractDynamicValues { get; set; }

    public virtual DbSet<ContractElectronicSignature> ContractElectronicSignatures { get; set; }

    public virtual DbSet<ContractHandoverRecord> ContractHandoverRecords { get; set; }

    public virtual DbSet<ContractNegotiationComment> ContractNegotiationComments { get; set; }

    public virtual DbSet<ContractPaymentMilestone> ContractPaymentMilestones { get; set; }

    public virtual DbSet<ContractPhoneVerify> ContractPhoneVerifies { get; set; }

    public virtual DbSet<ContractSignedEvidence> ContractSignedEvidences { get; set; }

    public virtual DbSet<ContractTemplate> ContractTemplates { get; set; }

    public virtual DbSet<ContractTemplateField> ContractTemplateFields { get; set; }

    public virtual DbSet<ContractTerm> ContractTerms { get; set; }

    public virtual DbSet<ContractVersion> ContractVersions { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerBusiness> CustomerBusinesses { get; set; }

    public virtual DbSet<CustomerInteraction> CustomerInteractions { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EmployeeDepartment> EmployeeDepartments { get; set; }

    public virtual DbSet<EmployeeRole> EmployeeRoles { get; set; }

    public virtual DbSet<FileStorage> FileStorages { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoicePromotionDetail> InvoicePromotionDetails { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationRecipient> NotificationRecipients { get; set; }

    public virtual DbSet<PackageLevel> PackageLevels { get; set; }

    public virtual DbSet<PackageLevelConversionRate> PackageLevelConversionRates { get; set; }

    public virtual DbSet<PackageLevelPermission> PackageLevelPermissions { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Plan> Plans { get; set; }

    public virtual DbSet<PlanVersion> PlanVersions { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<PromotionCustomerUsage> PromotionCustomerUsages { get; set; }

    public virtual DbSet<PromotionPlan> PromotionPlans { get; set; }

    public virtual DbSet<PromotionSaasType> PromotionSaasTypes { get; set; }

    public virtual DbSet<SaasPermission> SaasPermissions { get; set; }

    public virtual DbSet<SaasPermissionDependency> SaasPermissionDependencies { get; set; }

    public virtual DbSet<SaasPermissionGroup> SaasPermissionGroups { get; set; }

    public virtual DbSet<SaasType> SaasTypes { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<ServiceType> ServiceTypes { get; set; }

    public virtual DbSet<SystemPermission> SystemPermissions { get; set; }

    public virtual DbSet<SystemPermissionDependency> SystemPermissionDependencies { get; set; }

    public virtual DbSet<SystemPermissionGroup> SystemPermissionGroups { get; set; }

    public virtual DbSet<SystemRole> SystemRoles { get; set; }

    public virtual DbSet<SystemRolePermission> SystemRolePermissions { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<TenantDatabase> TenantDatabases { get; set; }

    public virtual DbSet<TicketMessage> TicketMessages { get; set; }

    public virtual DbSet<TicketSupport> TicketSupports { get; set; }

    public virtual DbSet<TicketSupportEmployee> TicketSupportEmployees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApprovalWorkflow>(entity =>
        {
            entity.ToTable("APPROVAL_WORKFLOW");

            entity.HasIndex(e => e.ApprovalWorkflowCode, "UQ__APPROVAL__F3A1A98475A85CC6").IsUnique();

            entity.Property(e => e.ApprovalWorkflowId).HasColumnName("approval_workflow_id");
            entity.Property(e => e.ApprovalWorkflowCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("approval_workflow_code");
            entity.Property(e => e.ApprovalWorkflowCreatedat)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("approval_workflow_createdat");
            entity.Property(e => e.ApprovalWorkflowCreatedbyemployeeid).HasColumnName("approval_workflow_createdbyemployeeid");
            entity.Property(e => e.ApprovalWorkflowDescription)
                .HasMaxLength(1000)
                .HasColumnName("approval_workflow_description");
            entity.Property(e => e.ApprovalWorkflowIsactive)
                .HasDefaultValue(true)
                .HasColumnName("approval_workflow_isactive");
            entity.Property(e => e.ApprovalWorkflowName)
                .HasMaxLength(200)
                .HasColumnName("approval_workflow_name");
            entity.Property(e => e.ApprovalWorkflowObjecttype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("approval_workflow_objecttype");
            entity.Property(e => e.ApprovalWorkflowUpdatedat).HasColumnName("approval_workflow_updatedat");
            entity.Property(e => e.ApprovalWorkflowUpdatedbyemployeeid).HasColumnName("approval_workflow_updatedbyemployeeid");
        });

        modelBuilder.Entity<ApprovalWorkflowStep>(entity =>
        {
            entity.ToTable("APPROVAL_WORKFLOW_STEP");

            entity.HasIndex(e => new { e.ApprovalWorkflowId, e.ApprovalWorkflowStepOrder }, "UQ_ApprovalWorkflowStep_Order").IsUnique();

            entity.Property(e => e.ApprovalWorkflowStepId).HasColumnName("approval_workflow_step_id");
            entity.Property(e => e.ApprovalWorkflowId).HasColumnName("approval_workflow_id");
            entity.Property(e => e.ApprovalWorkflowStepIsrequired)
                .HasDefaultValue(true)
                .HasColumnName("approval_workflow_step_isrequired");
            entity.Property(e => e.ApprovalWorkflowStepName)
                .HasMaxLength(200)
                .HasColumnName("approval_workflow_step_name");
            entity.Property(e => e.ApprovalWorkflowStepOrder).HasColumnName("approval_workflow_step_order");
            entity.Property(e => e.SystemRoleId).HasColumnName("system_role_id");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AUDIT_LO__6031F9F878CA986A");

            entity.ToTable("AUDIT_LOG");

            entity.HasIndex(e => new { e.ActorType, e.ActorId }, "IX_AuditLog_Actor");

            entity.HasIndex(e => new { e.EntityName, e.EntityId }, "IX_AuditLog_Entity");

            entity.Property(e => e.AuditLogId).HasColumnName("audit_log_id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("action_type");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.ActorIpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("actor_ip_address");
            entity.Property(e => e.ActorType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("actor_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_name");
            entity.Property(e => e.NewData).HasColumnName("new_data");
            entity.Property(e => e.OldData).HasColumnName("old_data");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("CATEGORIES");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryDesc).HasColumnName("category_desc");
            entity.Property(e => e.CategoryImage)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("category_image");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(500)
                .HasColumnName("category_name");
            entity.Property(e => e.CategoryParentid).HasColumnName("category_parentid");
            entity.Property(e => e.CategoryShortDesc)
                .HasMaxLength(1000)
                .HasColumnName("category_short_desc");
            entity.Property(e => e.CategorySortorder).HasColumnName("category_sortorder");
        });

        modelBuilder.Entity<ContractAccessLink>(entity =>
        {
            entity.ToTable("CONTRACT_ACCESS_LINK");

            entity.Property(e => e.ContractAccessLinkId).HasColumnName("contract_access_link_id");
            entity.Property(e => e.ActivatedAt).HasColumnName("activated_at");
            entity.Property(e => e.ContractAccessLinkRevocationReason)
                .HasMaxLength(1000)
                .HasColumnName("contract_access_link_revocation_reason");
            entity.Property(e => e.ContractAccessLinkTokenhash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("contract_access_link_tokenhash");
            entity.Property(e => e.ContractAccessLinkVerificationPhoneId).HasColumnName("contract_access_link_verification_phone_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByEmployeeId).HasColumnName("created_by_employee_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.RevokedByEmployeeId).HasColumnName("revoked_by_employee_id");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
        });

        modelBuilder.Entity<ContractApprovalRequest>(entity =>
        {
            entity.ToTable("CONTRACT_APPROVAL_REQUEST");

            entity.Property(e => e.ContractApprovalRequestId).HasColumnName("contract_approval_request_id");
            entity.Property(e => e.ApprovalWorkflowId).HasColumnName("approval_workflow_id");
            entity.Property(e => e.ContractApprovalRequestDecisioncomment)
                .HasMaxLength(1000)
                .HasColumnName("contract_approval_request_decisioncomment");
            entity.Property(e => e.ContractApprovalRequestResolvedAt).HasColumnName("contract_approval_request_resolved_at");
            entity.Property(e => e.ContractApprovalRequestRowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("contract_approval_request_rowversion");
            entity.Property(e => e.ContractApprovalRequestStatus).HasColumnName("contract_approval_request_status");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.ResolvedByEmployeeId).HasColumnName("resolved_by_employee_id");
            entity.Property(e => e.ContractReviewRequestId).HasColumnName("contract_review_request_id");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractApprovalRequest_SubmittedDate")
                .HasColumnName("submitted_at");
            entity.Property(e => e.SubmittedByEmployeeId).HasColumnName("submitted_by_employee_id");
        });

        modelBuilder.Entity<ContractDocument>(entity =>
        {
            entity.ToTable("CONTRACT_DOCUMENTS");

            entity.HasIndex(e => e.ContractTemplateId, "IX_ContractDocuments_TemplateId");

            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractDocumentCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("contract_document_code");
            entity.Property(e => e.ContractDocumentCurrentVersionId).HasColumnName("contract_document_current_version_id");
            entity.Property(e => e.ContractDocumentName)
                .HasMaxLength(1000)
                .HasColumnName("contract_document_name");
            entity.Property(e => e.ContractDocumentStatus).HasColumnName("contract_document_status");
            entity.Property(e => e.ContractDocumentType)
                .HasDefaultValue(1)
                .HasColumnName("contract_document_type");
            entity.Property(e => e.ContractTemplateId).HasColumnName("contract_template_id");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Doc_CreatedDate")
                .HasColumnName("created_date");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.TenantId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
        });

        modelBuilder.Entity<ContractDocumentFile>(entity =>
        {
            entity.ToTable("CONTRACT_DOCUMENT_FILES");

            entity.Property(e => e.ContractDocumentFileId).HasColumnName("contract_document_file_id");
            entity.Property(e => e.ContractDocumentFileRole).HasColumnName("contract_document_file_role");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractDocumentFiles_CreatedDate")
                .HasColumnName("created_date");
            entity.Property(e => e.CreatedEmployeeId).HasColumnName("created_employee_id");
            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
        });

        modelBuilder.Entity<ContractDocumentItem>(entity =>
        {
            entity.ToTable("CONTRACT_DOCUMENT_ITEMS");

            entity.HasIndex(e => e.ContractDocumentId, "IX_DocumentItems_DocumentId");

            entity.HasIndex(e => e.ProductId, "IX_DocumentItems_ProductId");

            entity.Property(e => e.ContractDocumentItemId).HasColumnName("contract_document_item_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractDocumentItemCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contract_document_item_code");
            entity.Property(e => e.ContractDocumentItemDiscountPercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("contract_document_item_discount_percent");
            entity.Property(e => e.ContractDocumentItemFixedDiscountAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_document_item_fixed_discount_amount");
            entity.Property(e => e.ContractDocumentItemLineSubtotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_document_item_line_subtotal");
            entity.Property(e => e.ContractDocumentItemName)
                .HasMaxLength(500)
                .HasColumnName("contract_document_item_name");
            entity.Property(e => e.ContractDocumentItemQuantity)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("contract_document_item_quantity");
            entity.Property(e => e.ContractDocumentItemUnitPrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_document_item_unit_price");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
        });

        modelBuilder.Entity<ContractDocumentRelation>(entity =>
        {
            entity.ToTable("CONTRACT_DOCUMENT_RELATION");

            entity.HasIndex(e => new { e.ParentDocumentId, e.ChildDocumentId }, "UQ_Parent_Child").IsUnique();

            entity.Property(e => e.ContractDocumentRelationId).HasColumnName("contract_document_relation_id");
            entity.Property(e => e.ChildDocumentId).HasColumnName("child_document_id");
            entity.Property(e => e.ContractDocumentRelationType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("contract_document_relation_type");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_date");
            entity.Property(e => e.ParentDocumentId).HasColumnName("parent_document_id");
        });

        modelBuilder.Entity<ContractDynamicValue>(entity =>
        {
            entity.ToTable("CONTRACT_DYNAMIC_VALUES");

            entity.HasIndex(e => e.ContractDocumentId, "IX_Dynamic_Values_DocumentId");

            entity.HasIndex(e => new { e.FieldKey, e.ContractDocumentId }, "IX_Dynamic_Values_Key_Doc");

            entity.Property(e => e.ContractDynamicValueId).HasColumnName("contract_dynamic_value_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.FieldKey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("field_key");
            entity.Property(e => e.FieldValue).HasColumnName("field_value");
        });

        modelBuilder.Entity<ContractElectronicSignature>(entity =>
        {
            entity.ToTable("CONTRACT_ELECTRONIC_SIGNATURE");

            entity.Property(e => e.ContractElectronicSignatureId).HasColumnName("contract_electronic_signature_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractElectronicCustomerotpchallengeid).HasColumnName("contract_electronic_customerotpchallengeid");
            entity.Property(e => e.ContractElectronicDocumentHashAtSigning)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("contract_electronic_document_hash_at_signing");
            entity.Property(e => e.ContractElectronicHeight)
                .HasColumnType("decimal(9, 2)")
                .HasColumnName("contract_electronic_height");
            entity.Property(e => e.ContractElectronicIpaddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("contract_electronic_ipaddress");
            entity.Property(e => e.ContractElectronicPagenumber).HasColumnName("contract_electronic_pagenumber");
            entity.Property(e => e.ContractElectronicPartytype).HasColumnName("contract_electronic_partytype");
            entity.Property(e => e.ContractElectronicPositionx)
                .HasColumnType("decimal(9, 2)")
                .HasColumnName("contract_electronic_positionx");
            entity.Property(e => e.ContractElectronicPositiony)
                .HasColumnType("decimal(9, 2)")
                .HasColumnName("contract_electronic_positiony");
            entity.Property(e => e.ContractElectronicRejectReason)
                .HasMaxLength(1000)
                .HasColumnName("contract_electronic_reject_reason");
            entity.Property(e => e.ContractElectronicSignatureimagestoragekey)
                .HasMaxLength(500)
                .HasColumnName("contract_electronic_signatureimagestoragekey");
            entity.Property(e => e.ContractElectronicSignaturetype)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAWN", "DF_CES_Type")
                .HasColumnName("contract_electronic_signaturetype");
            entity.Property(e => e.ContractElectronicSignedat).HasColumnName("contract_electronic_signedat");
            entity.Property(e => e.ContractElectronicSigneremail)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("contract_electronic_signeremail");
            entity.Property(e => e.ContractElectronicSignername)
                .HasMaxLength(200)
                .HasColumnName("contract_electronic_signername");
            entity.Property(e => e.ContractElectronicSignerphone)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("contract_electronic_signerphone");
            entity.Property(e => e.ContractElectronicSignertitle)
                .HasMaxLength(100)
                .HasColumnName("contract_electronic_signertitle");
            entity.Property(e => e.ContractElectronicSignertype)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("contract_electronic_signertype");
            entity.Property(e => e.ContractElectronicStatus).HasColumnName("contract_electronic_status");
            entity.Property(e => e.ContractElectronicUseragent)
                .HasMaxLength(1000)
                .HasColumnName("contract_electronic_useragent");
            entity.Property(e => e.ContractElectronicWidth)
                .HasColumnType("decimal(9, 2)")
                .HasColumnName("contract_electronic_width");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_CES_CreatedDate")
                .HasColumnName("created_at");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
        });

        modelBuilder.Entity<ContractHandoverRecord>(entity =>
        {
            entity.ToTable("CONTRACT_HANDOVER_RECORD");

            entity.Property(e => e.ContractHandoverRecordId).HasColumnName("contract_handover_record_id");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledByEmployeeId).HasColumnName("cancelled_by_employee_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractHandoverRecordCancellationreason)
                .HasMaxLength(1000)
                .HasColumnName("contract_handover_record_cancellationreason");
            entity.Property(e => e.ContractHandoverRecordContractsigndate).HasColumnName("contract_handover_record_contractsigndate");
            entity.Property(e => e.ContractHandoverRecordHandoverAt).HasColumnName("contract_handover_record_handover_at");
            entity.Property(e => e.ContractHandoverRecordHandovercode)
                .HasMaxLength(100)
                .HasColumnName("contract_handover_record_handovercode");
            entity.Property(e => e.ContractHandoverRecordLocation)
                .HasMaxLength(500)
                .HasColumnName("contract_handover_record_location");
            entity.Property(e => e.ContractHandoverRecordLocationen)
                .HasMaxLength(500)
                .HasColumnName("contract_handover_record_locationen");
            entity.Property(e => e.ContractHandoverRecordSnapshothash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("contract_handover_record_snapshothash");
            entity.Property(e => e.ContractHandoverRecordStatus).HasColumnName("contract_handover_record_status");
            entity.Property(e => e.ContractTemplateId).HasColumnName("contract_template_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedEmployeeId).HasColumnName("created_employee_id");
            entity.Property(e => e.FinalizedAt).HasColumnName("finalized_at");
            entity.Property(e => e.FinalizedByEmployeeId).HasColumnName("finalized_by_employee_id");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.SignedAt).HasColumnName("signed_at");
            entity.Property(e => e.SignedByEmployeeId).HasColumnName("signed_by_employee_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedEmployeeId).HasColumnName("updated_employee_id");
        });

        modelBuilder.Entity<ContractNegotiationComment>(entity =>
        {
            entity.ToTable("CONTRACT_NEGOTIATION_COMMENT");

            entity.Property(e => e.ContractNegotiationCommentId).HasColumnName("contract_negotiation_comment_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractNegotiationCommentCarriedforwardfromcommentid).HasColumnName("contract_negotiation_comment_carriedforwardfromcommentid");
            entity.Property(e => e.ContractNegotiationCommentCarriedforwardfromversionid).HasColumnName("contract_negotiation_comment_carriedforwardfromversionid");
            entity.Property(e => e.ContractNegotiationCommentContent)
                .HasMaxLength(4000)
                .HasColumnName("contract_negotiation_comment_content");
            entity.Property(e => e.ContractNegotiationCommentParentcommentid).HasColumnName("contract_negotiation_comment_parentcommentid");
            entity.Property(e => e.ContractNegotiationCommentSource)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ExternalFeedback")
                .HasColumnName("contract_negotiation_comment_source");
            entity.Property(e => e.ContractNegotiationCommentState).HasColumnName("contract_negotiation_comment_state");
            entity.Property(e => e.ContractNegotiationCommentTermid).HasColumnName("contract_negotiation_comment_termid");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerAccessSessionId).HasColumnName("customer_access_session_id");
            entity.Property(e => e.AuthorCustomerId).HasColumnName("author_customer_id");
            entity.Property(e => e.ContractReviewRequestId).HasColumnName("contract_review_request_id");
            entity.Property(e => e.RecordedByEmployeeId).HasColumnName("recorded_by_employee_id");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ContractPaymentMilestone>(entity =>
        {
            entity.ToTable("CONTRACT_PAYMENT_MILESTONE");

            entity.Property(e => e.ContractPaymentMilestoneId).HasColumnName("contract_payment_milestone_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractPaymentMilestoneAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_payment_milestone_amount");
            entity.Property(e => e.ContractPaymentMilestoneAnchordate).HasColumnName("contract_payment_milestone_anchordate");
            entity.Property(e => e.ContractPaymentMilestoneCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contract_payment_milestone_code");
            entity.Property(e => e.ContractPaymentMilestoneConditionen)
                .HasMaxLength(2000)
                .HasColumnName("contract_payment_milestone_conditionen");
            entity.Property(e => e.ContractPaymentMilestoneConditionvi)
                .HasMaxLength(2000)
                .HasColumnName("contract_payment_milestone_conditionvi");
            entity.Property(e => e.ContractPaymentMilestoneDaycountmode)
                .HasDefaultValue(1)
                .HasColumnName("contract_payment_milestone_daycountmode");
            entity.Property(e => e.ContractPaymentMilestoneDisplayorder).HasColumnName("contract_payment_milestone_displayorder");
            entity.Property(e => e.ContractPaymentMilestoneDueanchor)
                .HasDefaultValue(1)
                .HasColumnName("contract_payment_milestone_dueanchor");
            entity.Property(e => e.ContractPaymentMilestoneDuedate).HasColumnName("contract_payment_milestone_duedate");
            entity.Property(e => e.ContractPaymentMilestoneDueoffsetdays).HasColumnName("contract_payment_milestone_dueoffsetdays");
            entity.Property(e => e.ContractPaymentMilestonePaidat).HasColumnName("contract_payment_milestone_paidat");
            entity.Property(e => e.ContractPaymentMilestonePaidbyemployeeid).HasColumnName("contract_payment_milestone_paidbyemployeeid");
            entity.Property(e => e.ContractPaymentMilestonePaymentpercent)
                .HasColumnType("decimal(9, 4)")
                .HasColumnName("contract_payment_milestone_paymentpercent");
            entity.Property(e => e.ContractPaymentMilestonePaymentstatus).HasColumnName("contract_payment_milestone_paymentstatus");
            entity.Property(e => e.ContractPaymentMilestoneTitleen)
                .HasMaxLength(500)
                .HasColumnName("contract_payment_milestone_titleen");
            entity.Property(e => e.ContractPaymentMilestoneTitlevi)
                .HasMaxLength(500)
                .HasColumnName("contract_payment_milestone_titlevi");
            entity.Property(e => e.ContractTermId).HasColumnName("contract_term_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractPaymentMilestone_CreatedDate")
                .HasColumnName("created_at");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ContractPhoneVerify>(entity =>
        {
            entity.ToTable("CONTRACT_PHONE_VERIFY");

            entity.Property(e => e.ContractPhoneVerifyId).HasColumnName("contract_phone_verify_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractPhoneVerifyPhoneNumberNormalized)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasColumnName("contract_phone_verify_phone_number_normalized");
            entity.Property(e => e.ContractPhoneVerifyPhoneSource)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasColumnName("contract_phone_verify_phone_source");
            entity.Property(e => e.ContractPhoneVerifyReason)
                .HasMaxLength(1000)
                .HasColumnName("contract_phone_verify_reason");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByEmployeeid).HasColumnName("created_by_employeeid");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
        });

        modelBuilder.Entity<ContractSignedEvidence>(entity =>
        {
            entity.ToTable("CONTRACT_SIGNED_EVIDENCE");

            entity.Property(e => e.ContractSignedEvidenceId).HasColumnName("contract_signed_evidence_id");
            entity.Property(e => e.ContractDocumentFileId).HasColumnName("contract_document_file_id");
            entity.Property(e => e.ContractSignedEvidenceSigndate).HasColumnName("contract_signed_evidence_signdate");
            entity.Property(e => e.ContractSignedEvidenceStatus)
                .HasDefaultValue(1)
                .HasColumnName("contract_signed_evidence_status");
            entity.Property(e => e.ContractSignedEvidenceSupersedereason)
                .HasMaxLength(1000)
                .HasColumnName("contract_signed_evidence_supersedereason");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.SupersededAt).HasColumnName("superseded_at");
            entity.Property(e => e.SupersededByEmployeeId).HasColumnName("superseded_by_employee_id");
            entity.Property(e => e.SupersedesEvidenceId).HasColumnName("supersedes_evidence_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedByEmployeeid).HasColumnName("uploaded_by_employeeid");
        });

        modelBuilder.Entity<ContractTemplate>(entity =>
        {
            entity.ToTable("CONTRACT_TEMPLATE");

            entity.HasIndex(e => e.ContractTemplateCode, "UQ__CONTRACT__FF5A4CCA9BB3789D").IsUnique();

            entity.Property(e => e.ContractTemplateId).HasColumnName("contract_template_id");
            entity.Property(e => e.ContractTemplateCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("contract_template_code");
            entity.Property(e => e.ContractTemplateDescription)
                .HasMaxLength(2000)
                .HasColumnName("contract_template_description");
            entity.Property(e => e.ContractTemplateDocumentType).HasColumnName("contract_template_document_type");
            entity.Property(e => e.ContractTemplateLanguageMode)
                .HasDefaultValue(1)
                .HasColumnName("contract_template_language_mode");
            entity.Property(e => e.ContractTemplateName)
                .HasMaxLength(500)
                .HasColumnName("contract_template_name");
            entity.Property(e => e.ContractTemplateNameen)
                .HasMaxLength(500)
                .HasColumnName("contract_template_nameen");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractTemplate_CreatedDate")
                .HasColumnName("created_date");
            entity.Property(e => e.CreatedEmployeeId).HasColumnName("created_employee_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "DF_ContractTemplate_IsActive")
                .HasColumnName("is_active");
            entity.Property(e => e.ModifiedEmployeeId).HasColumnName("modified_employee_id");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
        });

        modelBuilder.Entity<ContractTemplateField>(entity =>
        {
            entity.ToTable("CONTRACT_TEMPLATE_FIELDS");

            entity.HasIndex(e => e.ContractTemplateId, "IX_TemplateFields_TemplateId");

            entity.Property(e => e.ContractTemplateFieldId).HasColumnName("contract_template_field_id");
            entity.Property(e => e.ContractTemplateFieldDataType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("TEXT")
                .HasColumnName("contract_template_field_data_type");
            entity.Property(e => e.ContractTemplateFieldKey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contract_template_field_key");
            entity.Property(e => e.ContractTemplateFieldLabel)
                .HasMaxLength(255)
                .HasColumnName("contract_template_field_label");
            entity.Property(e => e.ContractTemplateId).HasColumnName("contract_template_id");
            entity.Property(e => e.ExtraOptions).HasColumnName("extra_options");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
        });

        modelBuilder.Entity<ContractTerm>(entity =>
        {
            entity.HasKey(e => e.ContractTermTermid);

            entity.ToTable("CONTRACT_TERM");

            entity.Property(e => e.ContractTermTermid).HasColumnName("contract_term_termid");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractTermBilingualcontentmode)
                .HasDefaultValue(1)
                .HasColumnName("contract_term_bilingualcontentmode");
            entity.Property(e => e.ContractTermDisplayorder).HasColumnName("contract_term_displayorder");
            entity.Property(e => e.ContractTermIsnegotiable).HasColumnName("contract_term_isnegotiable");
            entity.Property(e => e.ContractTermSourcetemplatetermid).HasColumnName("contract_term_sourcetemplatetermid");
            entity.Property(e => e.ContractTermTermcode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("contract_term_termcode");
            entity.Property(e => e.ContractTermTermcontent).HasColumnName("contract_term_termcontent");
            entity.Property(e => e.ContractTermTermcontenten).HasColumnName("contract_term_termcontenten");
            entity.Property(e => e.ContractTermTermkind).HasColumnName("contract_term_termkind");
            entity.Property(e => e.ContractTermTermtitle)
                .HasMaxLength(500)
                .HasColumnName("contract_term_termtitle");
            entity.Property(e => e.ContractTermTermtitleen)
                .HasMaxLength(500)
                .HasColumnName("contract_term_termtitleen");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractTerm_CreatedDate")
                .HasColumnName("created_at");
            entity.Property(e => e.Rowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowversion");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.ToTable("CONTRACT_VERSION");

            entity.HasIndex(e => new { e.ContractDocumentId, e.ContractVersionVersionNo }, "UX_ContractVersion_Document_VersionNo").IsUnique();

            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractTemplateId).HasColumnName("contract_template_id");
            entity.Property(e => e.ContractVersionChangenote)
                .HasMaxLength(2000)
                .HasColumnName("contract_version_changenote");
            entity.Property(e => e.ContractVersionCreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContractVersion_CreatedDate")
                .HasColumnName("contract_version_created_date");
            entity.Property(e => e.ContractVersionCurrencycode)
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength()
                .HasDefaultValue("VND", "DF_ContractVersion_CurrencyCode")
                .HasColumnName("contract_version_currencycode");
            entity.Property(e => e.ContractVersionEffectiveDate).HasColumnName("contract_version_effective_date");
            entity.Property(e => e.ContractVersionExpireDate).HasColumnName("contract_version_expire_date");
            entity.Property(e => e.ContractVersionIslocked).HasColumnName("contract_version_islocked");
            entity.Property(e => e.ContractVersionLockedbyemployeeid).HasColumnName("contract_version_lockedbyemployeeid");
            entity.Property(e => e.ContractVersionLockeddate).HasColumnName("contract_version_lockeddate");
            entity.Property(e => e.ContractVersionRowversion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("contract_version_rowversion");
            entity.Property(e => e.ContractVersionSigndate).HasColumnName("contract_version_signdate");
            entity.Property(e => e.ContractVersionSnapshothash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("contract_version_snapshothash");
            entity.Property(e => e.ContractVersionSubtotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_version_subtotal");
            entity.Property(e => e.ContractVersionTotalamount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_version_totalamount");
            entity.Property(e => e.ContractVersionTotaldiscount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_version_totaldiscount");
            entity.Property(e => e.ContractVersionTotalvat)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("contract_version_totalvat");
            entity.Property(e => e.ContractVersionVersionNo).HasColumnName("contract_version_version_no");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__CUSTOMER__CD65CB85B5252E14");

            entity.ToTable("CUSTOMER");

            entity.HasIndex(e => e.CustomerEmail, "UQ_Customer_Email_NotNull").IsUnique().HasFilter("[customer_email] IS NOT NULL");

            entity.HasIndex(e => e.CustomerDisplayName, "UQ__CUSTOMER__DB022DF288B7F5EA").IsUnique();

            entity.HasIndex(e => e.CustomerPersonalPhone, "UQ__CUSTOMER__E223D5CF417E0019").IsUnique();

            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerAddress)
                .HasMaxLength(1000)
                .HasColumnName("customer_address");
            entity.Property(e => e.CustomerDisplayName)
                .HasMaxLength(200)
                .HasColumnName("customer_display_name");
            entity.Property(e => e.CustomerDob).HasColumnName("customer_dob");
            entity.Property(e => e.CustomerEmail)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("customer_email");
            entity.Property(e => e.CustomerEmailVerifiedAt).HasColumnName("customer_email_verified_at");
            entity.Property(e => e.CustomerFirstname)
                .HasMaxLength(100)
                .HasColumnName("customer_firstname");
            entity.Property(e => e.CustomerGender)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("customer_gender");
            entity.Property(e => e.CustomerLastLoginAt).HasColumnName("customer_last_login_at");
            entity.Property(e => e.CustomerLastname)
                .HasMaxLength(100)
                .HasColumnName("customer_lastname");
            entity.Property(e => e.CustomerMiddlename)
                .HasMaxLength(100)
                .HasColumnName("customer_middlename");
            entity.Property(e => e.CustomerPasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("customer_password_hash");
            entity.Property(e => e.CustomerPersonalPhone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("customer_personal_phone");
            entity.Property(e => e.CustomerPersonalPhoneVerifiedAt).HasColumnName("customer_personal_phone_verified_at");
            entity.Property(e => e.CustomerPersonalTaxIdentifier)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("customer_personal_tax_identifier");
            entity.Property(e => e.CustomerStatus)
                .HasDefaultValue(1)
                .HasColumnName("customer_status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<CustomerBusiness>(entity =>
        {
            entity.HasKey(e => e.CustomerBusinessId).HasName("PK__CUSTOMER__B2CF3EC59BDA0974");

            entity.ToTable("CUSTOMER_BUSINESS");

            entity.Property(e => e.CustomerBusinessId).HasColumnName("customer_business_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerBusinessCompanyTaxIdentifier)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("customer_business_company_tax_identifier");
            entity.Property(e => e.CustomerBusinessEmail)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("customer_business_email");
            entity.Property(e => e.CustomerBusinessHeadOfficeAddress)
                .HasMaxLength(1000)
                .HasColumnName("customer_business_head_office_address");
            entity.Property(e => e.CustomerBusinessLegalName)
                .HasMaxLength(1000)
                .HasColumnName("customer_business_legal_name");
            entity.Property(e => e.CustomerBusinessOfficeAddress)
                .HasMaxLength(1000)
                .HasColumnName("customer_business_office_address");
            entity.Property(e => e.CustomerBusinessOperationStartDate).HasColumnName("customer_business_operation_start_date");
            entity.Property(e => e.CustomerBusinessPhone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("customer_business_phone");
            entity.Property(e => e.CustomerBusinessRepresentativeName)
                .HasMaxLength(500)
                .HasColumnName("customer_business_representative_name");
            entity.Property(e => e.CustomerBusinessType)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("customer_business_type");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<CustomerInteraction>(entity =>
        {
            entity.ToTable("CUSTOMER_INTERACTION");

            entity.Property(e => e.CustomerInteractionId).HasColumnName("customer_interaction_id");
            entity.Property(e => e.CustomerInteractionInteractionAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("customer_interaction_interaction_at");
            entity.Property(e => e.CustomerInteractionInteractioncontent).HasColumnName("customer_interaction_interactioncontent");
            entity.Property(e => e.CustomerInteractionInteractionsubject)
                .HasMaxLength(1000)
                .HasColumnName("customer_interaction_interactionsubject");
            entity.Property(e => e.CustomerInteractionInteractiontype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("customer_interaction_interactiontype");
            entity.Property(e => e.CustomerInteractionNextfollowupdate)
                .HasColumnType("datetime")
                .HasColumnName("customer_interaction_nextfollowupdate");
            entity.Property(e => e.Customerid).HasColumnName("customerid");
            entity.Property(e => e.Employeeid).HasColumnName("employeeid");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__DEPARTME__C2232422C3D366D2");

            entity.ToTable("DEPARTMENT");

            entity.HasIndex(e => e.DepartmentName, "UQ__DEPARTME__226ED1570470B71D").IsUnique();

            entity.HasIndex(e => e.DepartmentCode, "UQ__DEPARTME__EBC3495EF6CC3992").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("department_code");
            entity.Property(e => e.DepartmentDescription)
                .HasMaxLength(2000)
                .HasColumnName("department_description");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(500)
                .HasColumnName("department_name");
            entity.Property(e => e.DepartmentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("department_status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__EMPLOYEE__C52E0BA8D2A57492");

            entity.ToTable("EMPLOYEE");

            entity.HasIndex(e => e.EmployeeEmail, "UQ__EMPLOYEE__0A874BCF8C73A05F").IsUnique();

            entity.HasIndex(e => e.EmployeeDisplayName, "UQ__EMPLOYEE__6A740DDF709E216C").IsUnique();

            entity.HasIndex(e => e.EmployeePhone, "UQ__EMPLOYEE__A10A0FADFA021982").IsUnique();

            entity.HasIndex(e => e.EmployeeCode, "UQ__EMPLOYEE__B0AA7345B6684A12").IsUnique();

            entity.HasIndex(e => e.EmployeePersonalIdentifier, "UQ__EMPLOYEE__D31ECC04EE0D8255").IsUnique();

            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmployeeAddress)
                .HasMaxLength(1000)
                .HasColumnName("employee_address");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmployeeDisplayName)
                .HasMaxLength(200)
                .HasColumnName("employee_display_name");
            entity.Property(e => e.EmployeeDob).HasColumnName("employee_dob");
            entity.Property(e => e.EmployeeEmail)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("employee_email");
            entity.Property(e => e.EmployeeEmailVerifiedAt).HasColumnName("employee_email_verified_at");
            entity.Property(e => e.EmployeeFirstName)
                .HasMaxLength(100)
                .HasColumnName("employee_first_name");
            entity.Property(e => e.EmployeeGender)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("employee_gender");
            entity.Property(e => e.EmployeeHandPhoneVerifiedAt).HasColumnName("employee_hand_phone_verified_at");
            entity.Property(e => e.EmployeeLastLoginAt).HasColumnName("employee_last_login_at");
            entity.Property(e => e.EmployeeLastName)
                .HasMaxLength(100)
                .HasColumnName("employee_last_name");
            entity.Property(e => e.EmployeeMiddleName)
                .HasMaxLength(100)
                .HasColumnName("employee_middle_name");
            entity.Property(e => e.EmployeePasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("employee_password_hash");
            entity.Property(e => e.EmployeePersonalIdentifier)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("employee_personal_identifier");
            entity.Property(e => e.EmployeePhone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("employee_phone");
            entity.Property(e => e.EmployeeStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("employee_status");
            entity.Property(e => e.EmployeeUsername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("employee_username");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<EmployeeDepartment>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("EMPLOYEE_DEPARTMENT");

            entity.HasIndex(e => new { e.EmployeeId, e.DepartmentId }, "EMPLOYEE_DEPARTMENT_index_1").IsUnique();

            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        });

        modelBuilder.Entity<EmployeeRole>(entity =>
        {
            entity.HasKey(e => e.EmployeeRoleId).HasName("PK__EMPLOYEE__AF99BE5BC0EB35CE");

            entity.ToTable("EMPLOYEE_ROLE");

            entity.HasIndex(e => new { e.EmployeeId, e.SystemRoleId }, "EMPLOYEE_ROLE_index_3").IsUnique();

            entity.Property(e => e.EmployeeRoleId).HasColumnName("employee_role_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.SystemMemberRoleAssignedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("system_member_role_assigned_at");
            entity.Property(e => e.SystemRoleId).HasColumnName("system_role_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<FileStorage>(entity =>
        {
            entity.HasKey(e => e.FileStorageFileid);

            entity.ToTable("FILE_STORAGE");

            entity.HasIndex(e => new { e.FileStorageObjecttype, e.FileStorageObjectid }, "IX_FileStorage_Object");

            entity.Property(e => e.FileStorageFileid).HasColumnName("file_storage_fileid");
            entity.Property(e => e.FileStorageContenttype)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("file_storage_contenttype");
            entity.Property(e => e.FileStorageFilename)
                .HasMaxLength(300)
                .HasColumnName("file_storage_filename");
            entity.Property(e => e.FileStorageFilepath)
                .HasMaxLength(1000)
                .HasColumnName("file_storage_filepath");
            entity.Property(e => e.FileStorageFilesize).HasColumnName("file_storage_filesize");
            entity.Property(e => e.FileStorageFiletype)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("file_storage_filetype");
            entity.Property(e => e.FileStorageObjectid).HasColumnName("file_storage_objectid");
            entity.Property(e => e.FileStorageObjecttype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("file_storage_objecttype");
            entity.Property(e => e.FileStorageSha256)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("file_storage_sha256");
            entity.Property(e => e.FileStorageStoragekey)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("file_storage_storagekey");
            entity.Property(e => e.FileStorageTenantcode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("file_storage_tenantcode");
            entity.Property(e => e.FileStorageUploadedbycustomerid).HasColumnName("file_storage_uploadedbycustomerid");
            entity.Property(e => e.FileStorageUploadedbyemployeeid).HasColumnName("file_storage_uploadedbyemployeeid");
            entity.Property(e => e.FileStorageUploadeddate)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_FileStorage_UploadedDate")
                .HasColumnType("datetime")
                .HasColumnName("file_storage_uploadeddate");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__INVOICE__F58DFD49BB754F96");

            entity.ToTable("INVOICE");

            entity.HasIndex(e => e.InvoiceCode, "UQ__INVOICE__5ED70A3522361665").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CustomerBusinessId).HasColumnName("customer_business_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.InvoiceBillingAddress)
                .HasMaxLength(1000)
                .HasColumnName("invoice_billing_address");
            entity.Property(e => e.InvoiceBillingEmail)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("invoice_billing_email");
            entity.Property(e => e.InvoiceBillingName)
                .HasMaxLength(500)
                .HasColumnName("invoice_billing_name");
            entity.Property(e => e.InvoiceBillingPhone)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("invoice_billing_phone");
            entity.Property(e => e.InvoiceBillingTaxIdentifier)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("invoice_billing_tax_identifier");
            entity.Property(e => e.InvoiceBuyerType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("INDIVIDUAL")
                .HasColumnName("invoice_buyer_type");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("invoice_code");
            entity.Property(e => e.InvoiceCurrency)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .HasColumnName("invoice_currency");
            entity.Property(e => e.InvoiceDiscountAmount)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("invoice_discount_amount");
            entity.Property(e => e.InvoiceIssuedAt).HasColumnName("invoice_issued_at");
            entity.Property(e => e.InvoicePaidAt).HasColumnName("invoice_paid_at");
            entity.Property(e => e.InvoiceStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("invoice_status");
            entity.Property(e => e.InvoiceSubtotal)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("invoice_subtotal");
            entity.Property(e => e.InvoiceTotalAmount)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("invoice_total_amount");
            entity.Property(e => e.IsTaxInvoiceRequested).HasColumnName("is_tax_invoice_requested");
            entity.Property(e => e.PlanVersionId).HasColumnName("plan_version_id");
            entity.Property(e => e.SubscriptionDays).HasColumnName("subscription_days");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<InvoicePromotionDetail>(entity =>
        {
            entity.HasKey(e => e.InvoicePromotionDetailId).HasName("PK__INVOICE___5610816CFA401B60");

            entity.ToTable("INVOICE_PROMOTION_DETAIL");

            entity.Property(e => e.InvoicePromotionDetailId).HasColumnName("invoice_promotion_detail_id");
            entity.Property(e => e.ActualMoneyDeducted)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("actual_money_deducted");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountValueApplied)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("discount_value_applied");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("NOTIFICATION");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.NotificationActionkey)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("notification_actionkey");
            entity.Property(e => e.NotificationBody)
                .HasMaxLength(1000)
                .HasColumnName("notification_body");
            entity.Property(e => e.NotificationCreatedatutc)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("notification_createdatutc");
            entity.Property(e => e.NotificationEventcode)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("notification_eventcode");
            entity.Property(e => e.NotificationEventkey)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("notification_eventkey");
            entity.Property(e => e.NotificationModulecode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("notification_modulecode");
            entity.Property(e => e.NotificationSubjectid).HasColumnName("notification_subjectid");
            entity.Property(e => e.NotificationSubjecttype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("notification_subjecttype");
            entity.Property(e => e.NotificationTitle)
                .HasMaxLength(300)
                .HasColumnName("notification_title");
        });

        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity.ToTable("NOTIFICATION_RECIPIENT");

            entity.Property(e => e.NotificationRecipientId).HasColumnName("notification_recipient_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.NotificationRecipientCreatedatutc)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("notification_recipient_createdatutc");
            entity.Property(e => e.NotificationRecipientDismissedatutc).HasColumnName("notification_recipient_dismissedatutc");
            entity.Property(e => e.NotificationRecipientReadatutc).HasColumnName("notification_recipient_readatutc");
        });

        modelBuilder.Entity<PackageLevel>(entity =>
        {
            entity.HasKey(e => e.PackageLevelId).HasName("PK__PACKAGE___9A4639DB9A593395");

            entity.ToTable("PACKAGE_LEVEL");

            entity.HasIndex(e => new { e.SaasTypeId, e.PackageLevelCode }, "PACKAGE_LEVEL_index_6").IsUnique();

            entity.Property(e => e.PackageLevelId).HasColumnName("package_level_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PackageLevelCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("package_level_code");
            entity.Property(e => e.PackageLevelDescription)
                .HasMaxLength(1000)
                .HasColumnName("package_level_description");
            entity.Property(e => e.PackageLevelName)
                .HasMaxLength(500)
                .HasColumnName("package_level_name");
            entity.Property(e => e.PackageLevelStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("package_level_status");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<PackageLevelConversionRate>(entity =>
        {
            entity.HasKey(e => e.PackageLevelConversionRateId).HasName("PK__PACKAGE___FAF451BAF19029E9");

            entity.ToTable("PACKAGE_LEVEL_CONVERSION_RATE");

            entity.HasIndex(e => new { e.SaasTypeId, e.FromLevelId, e.ToLevelId }, "PACKAGE_LEVEL_CONVERSION_RATE_index_7").IsUnique();

            entity.Property(e => e.PackageLevelConversionRateId).HasColumnName("package_level_conversion_rate_id");
            entity.Property(e => e.ConversionRate)
                .HasColumnType("decimal(18, 6)")
                .HasColumnName("conversion_rate");
            entity.Property(e => e.FromLevelId).HasColumnName("from_level_id");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.ToLevelId).HasColumnName("to_level_id");
        });

        modelBuilder.Entity<PackageLevelPermission>(entity =>
        {
            entity.HasKey(e => new { e.PackageLevelId, e.PermissionId }).HasName("PK__PACKAGE___24150874B0BEA0BA");

            entity.ToTable("PACKAGE_LEVEL_PERMISSION");

            entity.Property(e => e.PackageLevelId).HasColumnName("package_level_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__PAYMENT__ED1FC9EAE04952D8");

            entity.ToTable("PAYMENT");

            entity.HasIndex(e => e.PaymentReference, "UQ__PAYMENT__39A67FB2FE9668A0").IsUnique();

            entity.HasIndex(e => e.InvoiceId, "UQ__PAYMENT__F58DFD4891F9A57A").IsUnique();

            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.PaymentAmount)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("payment_amount");
            entity.Property(e => e.PaymentCurrency)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .HasColumnName("payment_currency");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentPaidAt).HasColumnName("payment_paid_at");
            entity.Property(e => e.PaymentProvider)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("payment_provider");
            entity.Property(e => e.PaymentProviderTransactionId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("payment_provider_transaction_id");
            entity.Property(e => e.PaymentReference)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("payment_reference");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SUCCESS")
                .HasColumnName("payment_status");
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasKey(e => e.PlanId).HasName("PK__PLAN__BE9F8F1D416C679E");

            entity.ToTable("PLAN");

            entity.HasIndex(e => new { e.SaasTypeId, e.PlanCode }, "PLAN_index_8").IsUnique();

            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CurrentActiveVersionId).HasColumnName("current_active_version_id");
            entity.Property(e => e.PackageLevelId).HasColumnName("package_level_id");
            entity.Property(e => e.PlanCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("plan_code");
            entity.Property(e => e.PlanName)
                .HasMaxLength(150)
                .HasColumnName("plan_name");
            entity.Property(e => e.PlanStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("plan_status");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<PlanVersion>(entity =>
        {
            entity.HasKey(e => e.PlanVersionId).HasName("PK__PLAN_VER__58158D544EB3C388");

            entity.ToTable("PLAN_VERSION");

            entity.HasIndex(e => new { e.PlanId, e.PlanVersionNumber }, "PLAN_VERSION_index_9").IsUnique();

            entity.Property(e => e.PlanVersionId).HasColumnName("plan_version_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.PlanVersionCurrency)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .HasColumnName("plan_version_currency");
            entity.Property(e => e.PlanVersionDurationDays).HasColumnName("plan_version_duration_days");
            entity.Property(e => e.PlanVersionEffectiveFrom).HasColumnName("plan_version_effective_from");
            entity.Property(e => e.PlanVersionEffectiveTo).HasColumnName("plan_version_effective_to");
            entity.Property(e => e.PlanVersionNumber).HasColumnName("plan_version_number");
            entity.Property(e => e.PlanVersionPrice)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("plan_version_price");
            entity.Property(e => e.PlanVersionStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("plan_version_status");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("PRODUCTS");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ProductCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("product_code");
            entity.Property(e => e.ProductCreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("product_created_at");
            entity.Property(e => e.ProductDeployment)
                .HasMaxLength(4000)
                .HasColumnName("product_deployment");
            entity.Property(e => e.ProductDocument)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("product_document");
            entity.Property(e => e.ProductIconclass)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("product_iconclass");
            entity.Property(e => e.ProductLargeimage)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("product_largeimage");
            entity.Property(e => e.ProductMetadescription)
                .HasMaxLength(2000)
                .HasColumnName("product_metadescription");
            entity.Property(e => e.ProductMetakeyword)
                .HasMaxLength(2000)
                .HasColumnName("product_metakeyword");
            entity.Property(e => e.ProductName)
                .HasMaxLength(500)
                .HasColumnName("product_name");
            entity.Property(e => e.ProductNameen)
                .HasMaxLength(500)
                .HasColumnName("product_nameen");
            entity.Property(e => e.ProductOthers)
                .HasMaxLength(4000)
                .HasColumnName("product_others");
            entity.Property(e => e.ProductQuantity).HasColumnName("product_quantity");
            entity.Property(e => e.ProductRewrite)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("product_rewrite");
            entity.Property(e => e.ProductShortdesc)
                .HasMaxLength(2000)
                .HasColumnName("product_shortdesc");
            entity.Property(e => e.ProductShortname)
                .HasMaxLength(100)
                .HasColumnName("product_shortname");
            entity.Property(e => e.ProductSlogan)
                .HasMaxLength(400)
                .HasColumnName("product_slogan");
            entity.Property(e => e.ProductSmallimage)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("product_smallimage");
            entity.Property(e => e.ProductSortorder).HasColumnName("product_sortorder");
            entity.Property(e => e.ProductSuggestedPrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("product_suggested_price");
            entity.Property(e => e.ProductTags)
                .HasMaxLength(500)
                .HasColumnName("product_tags");
            entity.Property(e => e.ProductTitlebrowser)
                .HasMaxLength(500)
                .HasColumnName("product_titlebrowser");
            entity.Property(e => e.ProductUnitname)
                .HasMaxLength(100)
                .HasColumnName("product_unitname");
            entity.Property(e => e.ProductUnitnameen)
                .HasMaxLength(100)
                .HasColumnName("product_unitnameen");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("PK__PROMOTIO__2CB9556B451F112C");

            entity.ToTable("PROMOTION");

            entity.HasIndex(e => e.PromotionCode, "UQ__PROMOTIO__888755056C56022D").IsUnique();

            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsStackable).HasColumnName("is_stackable");
            entity.Property(e => e.MaxUsagePerCustomer)
                .HasDefaultValue(1)
                .HasColumnName("max_usage_per_customer");
            entity.Property(e => e.PromotionCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("promotion_code");
            entity.Property(e => e.PromotionDescription)
                .HasMaxLength(1000)
                .HasColumnName("promotion_description");
            entity.Property(e => e.PromotionDiscountType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("promotion_discount_type");
            entity.Property(e => e.PromotionDiscountValue)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("promotion_discount_value");
            entity.Property(e => e.PromotionEndAt).HasColumnName("promotion_end_at");
            entity.Property(e => e.PromotionMaxDiscountAmount)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("promotion_max_discount_amount");
            entity.Property(e => e.PromotionName)
                .HasMaxLength(500)
                .HasColumnName("promotion_name");
            entity.Property(e => e.PromotionStartAt).HasColumnName("promotion_start_at");
            entity.Property(e => e.PromotionStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("promotion_status");
            entity.Property(e => e.PromotionUsageCount).HasColumnName("promotion_usage_count");
            entity.Property(e => e.PromotionUsageLimit).HasColumnName("promotion_usage_limit");
            entity.Property(e => e.StackingPriority).HasColumnName("stacking_priority");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<PromotionCustomerUsage>(entity =>
        {
            entity.HasKey(e => e.CustomerUsageId).HasName("PK__PROMOTIO__114EC9B955111466");

            entity.ToTable("PROMOTION_CUSTOMER_USAGE");

            entity.HasIndex(e => new { e.PromotionId, e.CustomerId }, "PROMOTION_CUSTOMER_USAGE_index_10").IsUnique();

            entity.Property(e => e.CustomerUsageId).HasColumnName("customer_usage_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.LastUsedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("last_used_at");
            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.TimesUsed).HasColumnName("times_used");
        });

        modelBuilder.Entity<PromotionPlan>(entity =>
        {
            entity.HasKey(e => new { e.PromotionId, e.PlanId }).HasName("PK__PROMOTIO__E750AD9AEC5C95DC");

            entity.ToTable("PROMOTION_PLAN");

            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.PlanId).HasColumnName("plan_id");
        });

        modelBuilder.Entity<PromotionSaasType>(entity =>
        {
            entity.HasKey(e => new { e.PromotionId, e.SaasTypeId }).HasName("PK__PROMOTIO__E333A6A88CCBE815");

            entity.ToTable("PROMOTION_SAAS_TYPE");

            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
        });

        modelBuilder.Entity<SaasPermission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__SAAS_PER__E5331AFA009E8BDE");

            entity.ToTable("SAAS_PERMISSION");

            entity.HasIndex(e => e.PermissionCode, "UQ__SAAS_PER__A98A808EF0CA6ED6").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PermissionCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("permission_code");
            entity.Property(e => e.PermissionDescription)
                .HasMaxLength(1000)
                .HasColumnName("permission_description");
            entity.Property(e => e.PermissionGroupId).HasColumnName("permission_group_id");
            entity.Property(e => e.PermissionName)
                .HasMaxLength(300)
                .HasColumnName("permission_name");
            entity.Property(e => e.PermissionPoint).HasColumnName("permission_point");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<SaasPermissionDependency>(entity =>
        {
            entity.HasKey(e => e.DependencyId).HasName("PK__SAAS_PER__952A18724CC2A10F");

            entity.ToTable("SAAS_PERMISSION_DEPENDENCY");

            entity.HasIndex(e => new { e.PermissionId, e.DependsOnPermissionId }, "SAAS_PERMISSION_DEPENDENCY_index_5").IsUnique();

            entity.Property(e => e.DependencyId).HasColumnName("dependency_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DependsOnPermissionId).HasColumnName("depends_on_permission_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
        });

        modelBuilder.Entity<SaasPermissionGroup>(entity =>
        {
            entity.HasKey(e => e.PermissionGroupId).HasName("PK__SAAS_PER__EE3284C6B100EDF5");

            entity.ToTable("SAAS_PERMISSION_GROUP");

            entity.HasIndex(e => new { e.SaasTypeId, e.GroupCode }, "SAAS_PERMISSION_GROUP_index_4").IsUnique();

            entity.Property(e => e.PermissionGroupId).HasColumnName("permission_group_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.GroupCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("group_code");
            entity.Property(e => e.GroupDescription)
                .HasMaxLength(1000)
                .HasColumnName("group_description");
            entity.Property(e => e.GroupIcon)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("group_icon");
            entity.Property(e => e.GroupName)
                .HasMaxLength(300)
                .HasColumnName("group_name");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<SaasType>(entity =>
        {
            entity.HasKey(e => e.SaasTypeId).HasName("PK__SAAS_TYP__F8AF3C3D5E0BB490");

            entity.ToTable("SAAS_TYPE");

            entity.HasIndex(e => e.SaasTypeCode, "UQ__SAAS_TYP__2C12F92018E4DBEF").IsUnique();

            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SaasTypeCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("saas_type_code");
            entity.Property(e => e.SaasTypeDescription)
                .HasMaxLength(2000)
                .HasColumnName("saas_type_description");
            entity.Property(e => e.SaasTypeName)
                .HasMaxLength(1000)
                .HasColumnName("saas_type_name");
            entity.Property(e => e.SaasTypeStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("saas_type_status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("SERVICES");

            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.ServiceDatecreated)
                .HasDefaultValueSql("(getdate())", "DF_tbl_Services_DateCreated")
                .HasColumnType("datetime")
                .HasColumnName("service_datecreated");
            entity.Property(e => e.ServiceDatemodified)
                .HasColumnType("datetime")
                .HasColumnName("service_datemodified");
            entity.Property(e => e.ServiceFptaccounts).HasColumnName("service_fptaccounts");
            entity.Property(e => e.ServiceHaschild).HasColumnName("service_haschild");
            entity.Property(e => e.ServiceImageicon)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("service_imageicon");
            entity.Property(e => e.ServiceMetadescription)
                .HasMaxLength(2000)
                .HasColumnName("service_metadescription");
            entity.Property(e => e.ServiceMetakeyword)
                .HasMaxLength(2000)
                .HasColumnName("service_metakeyword");
            entity.Property(e => e.ServiceMysql).HasColumnName("service_mysql");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(2000)
                .HasColumnName("service_name");
            entity.Property(e => e.ServiceNameen)
                .HasMaxLength(500)
                .HasColumnName("service_nameen");
            entity.Property(e => e.ServiceOthers)
                .HasMaxLength(4000)
                .HasColumnName("service_others");
            entity.Property(e => e.ServicePrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("service_price");
            entity.Property(e => e.ServiceRegion).HasColumnName("service_region");
            entity.Property(e => e.ServiceRewrite)
                .HasMaxLength(300)
                .IsUnicode(false)
                .HasColumnName("service_rewrite");
            entity.Property(e => e.ServiceStatus).HasColumnName("service_status");
            entity.Property(e => e.ServiceTitlebrowser)
                .HasMaxLength(500)
                .HasColumnName("service_titlebrowser");
            entity.Property(e => e.ServiceUnitname)
                .HasMaxLength(100)
                .HasColumnName("service_unitname");
            entity.Property(e => e.ServiceUnitnameen)
                .HasMaxLength(100)
                .HasColumnName("service_unitnameen");
            entity.Property(e => e.ServicetypeId).HasColumnName("servicetype_id");
        });

        modelBuilder.Entity<ServiceType>(entity =>
        {
            entity.ToTable("SERVICE_TYPES");

            entity.Property(e => e.ServicetypeId).HasColumnName("servicetype_id");
            entity.Property(e => e.ServicetypeName)
                .HasMaxLength(200)
                .HasColumnName("servicetype_name");
        });

        modelBuilder.Entity<SystemPermission>(entity =>
        {
            entity.HasKey(e => e.SystemPermissionId).HasName("PK__SYSTEM_P__F3CB9804FB5CA3FA");

            entity.ToTable("SYSTEM_PERMISSION");

            entity.HasIndex(e => e.SystemPermissionCode, "UQ__SYSTEM_P__8CA6D51699C21BCB").IsUnique();

            entity.Property(e => e.SystemPermissionId).HasColumnName("system_permission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.SystemPermissionAction)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("system_permission_action");
            entity.Property(e => e.SystemPermissionCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("system_permission_code");
            entity.Property(e => e.SystemPermissionDescription)
                .HasMaxLength(2000)
                .HasColumnName("system_permission_description");
            entity.Property(e => e.SystemPermissionGroupId).HasColumnName("system_permission_group_id");
            entity.Property(e => e.SystemPermissionName)
                .HasMaxLength(300)
                .HasColumnName("system_permission_name");
            entity.Property(e => e.SystemPermissionResource)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("system_permission_resource");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<SystemPermissionDependency>(entity =>
        {
            entity.HasKey(e => e.SystemPermissionDependencyId).HasName("PK__SYSTEM_P__27981FBC79A96D90");

            entity.ToTable("SYSTEM_PERMISSION_DEPENDENCY");

            entity.HasIndex(e => new { e.SystemPermissionId, e.DependsOnSystemPermissionId }, "SYSTEM_PERMISSION_DEPENDENCY_index_2").IsUnique();

            entity.Property(e => e.SystemPermissionDependencyId).HasColumnName("system_permission_dependency_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DependsOnSystemPermissionId).HasColumnName("depends_on_system_permission_id");
            entity.Property(e => e.SystemPermissionId).HasColumnName("system_permission_id");
        });

        modelBuilder.Entity<SystemPermissionGroup>(entity =>
        {
            entity.HasKey(e => e.SystemPermissionGroupId).HasName("PK__SYSTEM_P__6D5CBFFA5EDFC494");

            entity.ToTable("SYSTEM_PERMISSION_GROUP");

            entity.HasIndex(e => e.SystemPermissionGroupCode, "UQ__SYSTEM_P__2860628DAF4705C4").IsUnique();

            entity.Property(e => e.SystemPermissionGroupId).HasColumnName("system_permission_group_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.SystemPermissionGroupCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("system_permission_group_code");
            entity.Property(e => e.SystemPermissionGroupDescription)
                .HasMaxLength(2000)
                .HasColumnName("system_permission_group_description");
            entity.Property(e => e.SystemPermissionGroupName)
                .HasMaxLength(300)
                .HasColumnName("system_permission_group_name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<SystemRole>(entity =>
        {
            entity.HasKey(e => e.SystemRoleId).HasName("PK__SYSTEM_R__FC7047612A7DDCF3");

            entity.ToTable("SYSTEM_ROLE");

            entity.HasIndex(e => e.SystemRoleCode, "UQ__SYSTEM_R__D218DF4458F688EE").IsUnique();

            entity.Property(e => e.SystemRoleId).HasColumnName("system_role_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.SystemRoleCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("system_role_code");
            entity.Property(e => e.SystemRoleDescription)
                .HasMaxLength(500)
                .HasColumnName("system_role_description");
            entity.Property(e => e.SystemRoleName)
                .HasMaxLength(200)
                .HasColumnName("system_role_name");
            entity.Property(e => e.SystemRoleScope)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SYSTEM")
                .HasColumnName("system_role_scope");
            entity.Property(e => e.SystemRoleStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("system_role_status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<SystemRolePermission>(entity =>
        {
            entity.HasKey(e => new { e.SystemRoleId, e.SystemPermissionId }).HasName("PK__SYSTEM_R__A34CFEE194E144AA");

            entity.ToTable("SYSTEM_ROLE_PERMISSION");

            entity.Property(e => e.SystemRoleId).HasColumnName("system_role_id");
            entity.Property(e => e.SystemPermissionId).HasColumnName("system_permission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.TenantId).HasName("PK__TENANT__D6F29F3E0FE3142F");

            entity.ToTable("TENANT");

            entity.HasIndex(e => e.TenantCode, "UQ__TENANT__6568FE09D12EE88D").IsUnique();

            entity.HasIndex(e => e.TenantSubdomain, "UQ__TENANT__9357B5AACADDD440").IsUnique();

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.PackageLevelId).HasColumnName("package_level_id");
            entity.Property(e => e.SaasTypeId).HasColumnName("saas_type_id");
            entity.Property(e => e.TenantCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("tenant_code");
            entity.Property(e => e.TenantExpiredAt).HasColumnName("tenant_expired_at");
            entity.Property(e => e.TenantName)
                .HasMaxLength(500)
                .HasColumnName("tenant_name");
            entity.Property(e => e.TenantStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("tenant_status");
            entity.Property(e => e.TenantSubdomain)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("tenant_subdomain");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TenantDatabase>(entity =>
        {
            entity.HasKey(e => e.TenantDatabaseId).HasName("PK__TENANT_D__F437F196D222B0BF");

            entity.ToTable("TENANT_DATABASE");

            entity.HasIndex(e => e.TenantId, "UQ__TENANT_D__D6F29F3F52FBA030").IsUnique();

            entity.Property(e => e.TenantDatabaseId).HasColumnName("tenant_database_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.TenantDatabaseDbName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("tenant_database_db_name");
            entity.Property(e => e.TenantDatabaseHost)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("tenant_database_host");
            entity.Property(e => e.TenantDatabasePasswordEncrypted)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("tenant_database_password_encrypted");
            entity.Property(e => e.TenantDatabasePort).HasColumnName("tenant_database_port");
            entity.Property(e => e.TenantDatabaseProvider)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("tenant_database_provider");
            entity.Property(e => e.TenantDatabaseProvisioningStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("FRESH")
                .HasColumnName("tenant_database_provisioning_status");
            entity.Property(e => e.TenantDatabaseUser)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("tenant_database_user");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(e => e.TicketMessageId).HasName("PK__TICKET_M__90FF0B6528C008EC");

            entity.ToTable("TICKET_MESSAGE");

            entity.Property(e => e.TicketMessageId).HasColumnName("ticket_message_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.TicketMessageContent).HasColumnName("ticket_message_content");
            entity.Property(e => e.TicketMessageSenderCustomerId).HasColumnName("ticket_message_sender_customer_id");
            entity.Property(e => e.TicketMessageSenderEmployeeId).HasColumnName("ticket_message_sender_employee_id");
            entity.Property(e => e.TicketMessageType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ticket_message_type");
            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
        });

        modelBuilder.Entity<TicketSupport>(entity =>
        {
            entity.HasKey(e => e.TicketSupportId).HasName("PK__TICKET_S__30F865D0C4B976A9");

            entity.ToTable("TICKET_SUPPORT");

            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TicketClosedAt)
                .HasColumnType("datetime")
                .HasColumnName("ticket_closed_at");
            entity.Property(e => e.TicketCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("ticket_code");
            entity.Property(e => e.TicketDescription).HasColumnName("ticket_description");
            entity.Property(e => e.TicketPriority)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ticket_priority");
            entity.Property(e => e.TicketResolution).HasColumnName("ticket_resolution");
            entity.Property(e => e.TicketResolvedAt)
                .HasColumnType("datetime")
                .HasColumnName("ticket_resolved_at");
            entity.Property(e => e.TicketStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("ticket_status");
            entity.Property(e => e.TicketTitle)
                .HasMaxLength(1000)
                .HasColumnName("ticket_title");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<TicketSupportEmployee>(entity =>
        {
            entity.HasKey(e => e.TicketSupportEmployeeId).HasName("PK__TICKET_S__8C600BDF0093E4A7");

            entity.ToTable("TICKET_SUPPORT_EMPLOYEE");

            entity.Property(e => e.TicketSupportEmployeeId).HasColumnName("ticket_support_employee_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.TicketSupportEmployeeAssignedAt)
                .HasColumnType("datetime")
                .HasColumnName("ticket_support_employee_assigned_at");
            entity.Property(e => e.TicketSupportEmployeeAssignedByEmployeeId).HasColumnName("ticket_support_employee_assigned_by_employee_id");
            entity.Property(e => e.TicketSupportEmployeeAssignmentRole)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ticket_support_employee_assignment_role");
            entity.Property(e => e.TicketSupportEmployeeUnassignedAt)
                .HasColumnType("datetime")
                .HasColumnName("ticket_support_employee_unassigned_at");
            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
        });

        modelBuilder.Entity<ContractReviewRequest>(entity =>
        {
            entity.ToTable("CONTRACT_REVIEW_REQUEST");

            entity.HasIndex(e => new { e.ContractDocumentId, e.SentAt }, "IX_ReviewRequest_Document");
            entity.HasIndex(e => new { e.CustomerId, e.ContractReviewRequestStatus }, "IX_ReviewRequest_Customer_Status");

            entity.Property(e => e.ContractReviewRequestId).HasColumnName("contract_review_request_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.ContractVersionId).HasColumnName("contract_version_id");
            entity.Property(e => e.ContractReviewRequestDocumentHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().HasColumnName("contract_review_request_document_hash");
            entity.Property(e => e.PreviousReviewRequestId).HasColumnName("previous_review_request_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ContractReviewRequestMessage).HasMaxLength(1000).HasColumnName("contract_review_request_message");
            entity.Property(e => e.SentByEmployeeId).HasColumnName("sent_by_employee_id");
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ReviewRequest_SentAt").HasColumnName("sent_at");
            entity.Property(e => e.ContractReviewRequestStatus).HasMaxLength(30).IsUnicode(false).HasColumnName("contract_review_request_status");
            entity.Property(e => e.FirstViewedAt).HasColumnName("first_viewed_at");
            entity.Property(e => e.ContractReviewRequestCustomerNote).HasMaxLength(4000).HasColumnName("contract_review_request_customer_note");
            entity.Property(e => e.RespondedByCustomerId).HasColumnName("responded_by_customer_id");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.RespondedIpAddress).HasMaxLength(45).IsUnicode(false).HasColumnName("responded_ip_address");
            entity.Property(e => e.RespondedUserAgent).HasMaxLength(1000).HasColumnName("responded_user_agent");
            entity.Property(e => e.ContractReviewRequestFollowupDecision).HasMaxLength(20).IsUnicode(false).HasColumnName("contract_review_request_followup_decision");
            entity.Property(e => e.ContractReviewRequestFollowupNote).HasMaxLength(2000).HasColumnName("contract_review_request_followup_note");
            entity.Property(e => e.FollowupDecidedByEmployeeId).HasColumnName("followup_decided_by_employee_id");
            entity.Property(e => e.FollowupDecidedAt).HasColumnName("followup_decided_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ReviewRequest_CreatedAt").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Rowversion).IsRowVersion().IsConcurrencyToken().HasColumnName("rowversion");
        });

        modelBuilder.Entity<SmsOutbox>(entity =>
        {
            entity.ToTable("SMS_OUTBOX");

            entity.HasIndex(e => e.SmsOutboxRequestId, "UQ_SmsOutbox_RequestId").IsUnique();

            entity.Property(e => e.SmsOutboxId).HasColumnName("sms_outbox_id");
            entity.Property(e => e.SmsOutboxPurpose).HasMaxLength(30).IsUnicode(false).HasColumnName("sms_outbox_purpose");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ContractDocumentId).HasColumnName("contract_document_id");
            entity.Property(e => e.SmsOutboxPhoneCiphertext).HasMaxLength(512).HasColumnName("sms_outbox_phone_ciphertext");
            entity.Property(e => e.SmsOutboxContentCiphertext).HasMaxLength(2048).HasColumnName("sms_outbox_content_ciphertext");
            entity.Property(e => e.SmsOutboxPhoneMasked).HasMaxLength(20).IsUnicode(false).HasColumnName("sms_outbox_phone_masked");
            entity.Property(e => e.SmsOutboxDeliveryExpiresAt).HasColumnName("sms_outbox_delivery_expires_at");
            entity.Property(e => e.SmsOutboxStatus).HasMaxLength(20).IsUnicode(false).HasColumnName("sms_outbox_status");
            entity.Property(e => e.SmsOutboxAttemptCount).HasColumnName("sms_outbox_attempt_count");
            entity.Property(e => e.SmsOutboxNextAttemptAt).HasColumnName("sms_outbox_next_attempt_at");
            entity.Property(e => e.SmsOutboxLeaseId).HasColumnName("sms_outbox_lease_id");
            entity.Property(e => e.SmsOutboxLeaseUntil).HasColumnName("sms_outbox_lease_until");
            entity.Property(e => e.SmsOutboxSentAt).HasColumnName("sms_outbox_sent_at");
            entity.Property(e => e.SmsOutboxFailedAt).HasColumnName("sms_outbox_failed_at");
            entity.Property(e => e.SmsOutboxLastFailure).HasMaxLength(1000).HasColumnName("sms_outbox_last_failure");
            entity.Property(e => e.SmsOutboxProvider).HasMaxLength(30).IsUnicode(false).HasColumnName("sms_outbox_provider");
            entity.Property(e => e.SmsOutboxRequestId).HasMaxLength(50).IsUnicode(false).HasColumnName("sms_outbox_request_id");
            entity.Property(e => e.SmsOutboxBrandname).HasMaxLength(50).IsUnicode(false).HasColumnName("sms_outbox_brandname");
            entity.Property(e => e.SmsOutboxIsSandbox).HasColumnName("sms_outbox_is_sandbox");
            entity.Property(e => e.SmsOutboxCodeResult).HasMaxLength(10).IsUnicode(false).HasColumnName("sms_outbox_code_result");
            entity.Property(e => e.SmsOutboxProviderSmsId).HasMaxLength(100).IsUnicode(false).HasColumnName("sms_outbox_provider_sms_id");
            entity.Property(e => e.SmsOutboxSendStatus).HasMaxLength(5).IsUnicode(false).HasColumnName("sms_outbox_send_status");
            entity.Property(e => e.SmsOutboxSendSuccess).HasColumnName("sms_outbox_send_success");
            entity.Property(e => e.SmsOutboxSendFailed).HasColumnName("sms_outbox_send_failed");
            entity.Property(e => e.SmsOutboxTelcoId).HasMaxLength(10).IsUnicode(false).HasColumnName("sms_outbox_telco_id");
            entity.Property(e => e.SmsOutboxTotalPrice).HasColumnType("decimal(18, 4)").HasColumnName("sms_outbox_total_price");
            entity.Property(e => e.SmsOutboxDeliveredAt).HasColumnName("sms_outbox_delivered_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_SmsOutbox_CreatedAt").HasColumnName("created_at");
            entity.Property(e => e.Rowversion).IsRowVersion().IsConcurrencyToken().HasColumnName("rowversion");
        });

        modelBuilder.Entity<SmsProviderCallback>(entity =>
        {
            entity.ToTable("SMS_PROVIDER_CALLBACK");

            entity.Property(e => e.SmsProviderCallbackId).HasColumnName("sms_provider_callback_id");
            entity.Property(e => e.SmsOutboxId).HasColumnName("sms_outbox_id");
            entity.Property(e => e.SmsProviderCallbackProviderSmsId).HasMaxLength(100).IsUnicode(false).HasColumnName("sms_provider_callback_provider_sms_id");
            entity.Property(e => e.SmsProviderCallbackRequestId).HasMaxLength(50).IsUnicode(false).HasColumnName("sms_provider_callback_request_id");
            entity.Property(e => e.SmsProviderCallbackSendStatus).HasMaxLength(5).IsUnicode(false).HasColumnName("sms_provider_callback_send_status");
            entity.Property(e => e.SmsProviderCallbackRawQuery).HasMaxLength(4000).HasColumnName("sms_provider_callback_raw_query");
            entity.Property(e => e.SmsProviderCallbackSourceIp).HasMaxLength(45).IsUnicode(false).HasColumnName("sms_provider_callback_source_ip");
            entity.Property(e => e.SmsProviderCallbackIsProcessed).HasColumnName("sms_provider_callback_is_processed");
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_SmsCallback_ReceivedAt").HasColumnName("received_at");
        });

        // DDL defines a NOT NULL unique pair, but no physical primary key.
        // Use that existing uniqueness for EF tracking without changing the database.
        modelBuilder.Entity<EmployeeDepartment>()
            .HasKey(x => new { x.EmployeeId, x.DepartmentId });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
