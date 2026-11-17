using System;
using System.Collections.Generic;
using ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered;

public partial class LegacyConTrackDbContext : DbContext
{
    public LegacyConTrackDbContext(DbContextOptions<LegacyConTrackDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<APPROVAL_WORKFLOW> APPROVAL_WORKFLOWs { get; set; }

    public virtual DbSet<APPROVAL_WORKFLOW_STEP> APPROVAL_WORKFLOW_STEPs { get; set; }

    public virtual DbSet<AUDIT_LOG> AUDIT_LOGs { get; set; }

    public virtual DbSet<CATEGORy> CATEGORIEs { get; set; }

    public virtual DbSet<CONTRACT> CONTRACTs { get; set; }

    public virtual DbSet<CONTRACT_ACCESS_LINK> CONTRACT_ACCESS_LINKs { get; set; }

    public virtual DbSet<CONTRACT_APPENDix> CONTRACT_APPENDICEs { get; set; }

    public virtual DbSet<CONTRACT_APPROVAL_REQUEST> CONTRACT_APPROVAL_REQUESTs { get; set; }

    public virtual DbSet<CONTRACT_DOCUMENT> CONTRACT_DOCUMENTs { get; set; }

    public virtual DbSet<CONTRACT_DOCUMENT_FILE> CONTRACT_DOCUMENT_FILEs { get; set; }

    public virtual DbSet<CONTRACT_DOCUMENT_RELATION> CONTRACT_DOCUMENT_RELATIONs { get; set; }

    public virtual DbSet<CONTRACT_ELECTRONIC_SIGNATURE> CONTRACT_ELECTRONIC_SIGNATUREs { get; set; }

    public virtual DbSet<CONTRACT_HANDOVER_RECORD> CONTRACT_HANDOVER_RECORDs { get; set; }

    public virtual DbSet<CONTRACT_NEGOTIATION_COMMENT> CONTRACT_NEGOTIATION_COMMENTs { get; set; }

    public virtual DbSet<CONTRACT_PAYMENT_MILESTONE> CONTRACT_PAYMENT_MILESTONEs { get; set; }

    public virtual DbSet<CONTRACT_PHONE_VERIFY> CONTRACT_PHONE_VERIFies { get; set; }

    public virtual DbSet<CONTRACT_RENEWAL> CONTRACT_RENEWALs { get; set; }

    public virtual DbSet<CONTRACT_SIGNED_EVIDENCE> CONTRACT_SIGNED_EVIDENCEs { get; set; }

    public virtual DbSet<CONTRACT_TEMPLATE> CONTRACT_TEMPLATEs { get; set; }

    public virtual DbSet<CONTRACT_TERM> CONTRACT_TERMs { get; set; }

    public virtual DbSet<CONTRACT_TYPE> CONTRACT_TYPEs { get; set; }

    public virtual DbSet<CONTRACT_VERSION> CONTRACT_VERSIONs { get; set; }

    public virtual DbSet<CUSTOMER> CUSTOMERs { get; set; }

    public virtual DbSet<CUSTOMER_BUSINESS> CUSTOMER_BUSINESSes { get; set; }

    public virtual DbSet<CUSTOMER_INTERACTION> CUSTOMER_INTERACTIONs { get; set; }

    public virtual DbSet<DEPARTMENT> DEPARTMENTs { get; set; }

    public virtual DbSet<DOCUMENT_TYPE> DOCUMENT_TYPEs { get; set; }

    public virtual DbSet<EMPLOYEE> EMPLOYEEs { get; set; }

    public virtual DbSet<EMPLOYEE_DEPARTMENT> EMPLOYEE_DEPARTMENTs { get; set; }

    public virtual DbSet<EMPLOYEE_ROLE> EMPLOYEE_ROLEs { get; set; }

    public virtual DbSet<FILE_STORAGE> FILE_STORAGEs { get; set; }

    public virtual DbSet<INVOICE> INVOICEs { get; set; }

    public virtual DbSet<INVOICE_PROMOTION_DETAIL> INVOICE_PROMOTION_DETAILs { get; set; }

    public virtual DbSet<NOTIFICATION> NOTIFICATIONs { get; set; }

    public virtual DbSet<NOTIFICATION_RECIPIENT> NOTIFICATION_RECIPIENTs { get; set; }

    public virtual DbSet<OCR_BATCH> OCR_BATCHes { get; set; }

    public virtual DbSet<OCR_JOB> OCR_JOBs { get; set; }

    public virtual DbSet<PACKAGE_LEVEL> PACKAGE_LEVELs { get; set; }

    public virtual DbSet<PACKAGE_LEVEL_CONVERSION_RATE> PACKAGE_LEVEL_CONVERSION_RATEs { get; set; }

    public virtual DbSet<PACKAGE_LEVEL_PERMISSION> PACKAGE_LEVEL_PERMISSIONs { get; set; }

    public virtual DbSet<PAYMENT> PAYMENTs { get; set; }

    public virtual DbSet<PLAN> PLANs { get; set; }

    public virtual DbSet<PLAN_VERSION> PLAN_VERSIONs { get; set; }

    public virtual DbSet<PRODUCT> PRODUCTs { get; set; }

    public virtual DbSet<PROMOTION> PROMOTIONs { get; set; }

    public virtual DbSet<PROMOTION_CUSTOMER_USAGE> PROMOTION_CUSTOMER_USAGEs { get; set; }

    public virtual DbSet<PROMOTION_PLAN> PROMOTION_PLANs { get; set; }

    public virtual DbSet<PROMOTION_SAAS_TYPE> PROMOTION_SAAS_TYPEs { get; set; }

    public virtual DbSet<SAAS_PERMISSION> SAAS_PERMISSIONs { get; set; }

    public virtual DbSet<SAAS_PERMISSION_DEPENDENCY> SAAS_PERMISSION_DEPENDENCies { get; set; }

    public virtual DbSet<SAAS_PERMISSION_GROUP> SAAS_PERMISSION_GROUPs { get; set; }

    public virtual DbSet<SAAS_TYPE> SAAS_TYPEs { get; set; }

    public virtual DbSet<SERVICE> SERVICEs { get; set; }

    public virtual DbSet<SERVICE_TYPE> SERVICE_TYPEs { get; set; }

    public virtual DbSet<SYSTEM_PERMISSION> SYSTEM_PERMISSIONs { get; set; }

    public virtual DbSet<SYSTEM_PERMISSION_DEPENDENCY> SYSTEM_PERMISSION_DEPENDENCies { get; set; }

    public virtual DbSet<SYSTEM_PERMISSION_GROUP> SYSTEM_PERMISSION_GROUPs { get; set; }

    public virtual DbSet<SYSTEM_ROLE> SYSTEM_ROLEs { get; set; }

    public virtual DbSet<SYSTEM_ROLE_PERMISSION> SYSTEM_ROLE_PERMISSIONs { get; set; }

    public virtual DbSet<TENANT> TENANTs { get; set; }

    public virtual DbSet<TENANT_DATABASE> TENANT_Databases { get; set; }

    public virtual DbSet<TICKET_ATTACHMENT> TICKET_ATTACHMENTs { get; set; }

    public virtual DbSet<TICKET_HISTORY> TICKET_HISTORies { get; set; }

    public virtual DbSet<TICKET_MESSAGE> TICKET_MESSAGEs { get; set; }

    public virtual DbSet<TICKET_SUPPORT> TICKET_SUPPORTs { get; set; }

    public virtual DbSet<TICKET_SUPPORT_EMPLOYEE> TICKET_SUPPORT_EMPLOYEEs { get; set; }

    public virtual DbSet<TICKET_TYPE> TICKET_TYPEs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<APPROVAL_WORKFLOW>(entity =>
        {
            entity.HasKey(e => e.approval_workflow_id).HasName("PK__APPROVAL__FDC5955111BB6CB0");

            entity.ToTable("APPROVAL_WORKFLOW");

            entity.HasIndex(e => e.approval_workflow_code, "UQ__APPROVAL__F3A1A98445CF6794").IsUnique();

            entity.Property(e => e.approval_workflow_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.approval_workflow_createdat).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.approval_workflow_description).HasMaxLength(1000);
            entity.Property(e => e.approval_workflow_isactive).HasDefaultValue(true);
            entity.Property(e => e.approval_workflow_name).HasMaxLength(200);
            entity.Property(e => e.approval_workflow_objecttype)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<APPROVAL_WORKFLOW_STEP>(entity =>
        {
            entity.HasKey(e => e.approval_workflow_step_id).HasName("PK__APPROVAL__851F8ED5C493FD40");

            entity.ToTable("APPROVAL_WORKFLOW_STEP");

            entity.Property(e => e.approval_workflow_step_isrequired).HasDefaultValue(true);
            entity.Property(e => e.approval_workflow_step_name).HasMaxLength(200);
        });

        modelBuilder.Entity<AUDIT_LOG>(entity =>
        {
            entity.HasKey(e => e.audit_log_id).HasName("PK__AUDIT_LO__6031F9F8D76A3D9A");

            entity.ToTable("AUDIT_LOG");

            entity.HasIndex(e => new { e.actor_type, e.actor_id }, "IX_AuditLog_Actor");

            entity.HasIndex(e => new { e.entity_name, e.entity_id }, "IX_AuditLog_Entity");

            entity.Property(e => e.action_type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.actor_ip_address)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.actor_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.entity_id)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.entity_name)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CATEGORy>(entity =>
        {
            entity.HasKey(e => e.category_id).HasName("PK__CATEGORI__D54EE9B44506C57D");

            entity.ToTable("CATEGORIES");

            entity.Property(e => e.category_image)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.category_name).HasMaxLength(500);
            entity.Property(e => e.category_short_desc).HasMaxLength(1000);
        });

        modelBuilder.Entity<CONTRACT>(entity =>
        {
            entity.HasKey(e => e.contract_id).HasName("PK__CONTRACT__F8D66423DBE3A4B3");

            entity.ToTable("CONTRACTS");

            entity.HasIndex(e => e.contract_code, "UQ__CONTRACT__68CFBDBAFCE9EC10").IsUnique();

            entity.Property(e => e.approval_status).HasMaxLength(200);
            entity.Property(e => e.contract_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.contract_description).HasMaxLength(2000);
            entity.Property(e => e.contract_name).HasMaxLength(500);
            entity.Property(e => e.contract_nameen).HasMaxLength(500);
            entity.Property(e => e.contract_status).HasMaxLength(200);
            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.currency)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.effective_date).HasColumnType("datetime");
            entity.Property(e => e.expiration_date).HasColumnType("datetime");
            entity.Property(e => e.signed_date).HasColumnType("datetime");
            entity.Property(e => e.source_type)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CONTRACT_ACCESS_LINK>(entity =>
        {
            entity.HasKey(e => e.contract_access_link_id).HasName("PK__CONTRACT__F501F77ECEAE6D9E");

            entity.ToTable("CONTRACT_ACCESS_LINK");

            entity.Property(e => e.contract_access_link_revocation_reason).HasMaxLength(1000);
            entity.Property(e => e.contract_access_link_tokenhash)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_APPENDix>(entity =>
        {
            entity.HasKey(e => e.contract_appendix_id).HasName("PK__CONTRACT__4F13C612660607FE");

            entity.ToTable("CONTRACT_APPENDICES");

            entity.Property(e => e.contract_appendix_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.contract_appendix_created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.contract_appendix_description).HasMaxLength(2000);
            entity.Property(e => e.contract_appendix_name).HasMaxLength(500);
            entity.Property(e => e.contract_version_rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_APPROVAL_REQUEST>(entity =>
        {
            entity.HasKey(e => e.contract_approval_request_id).HasName("PK__CONTRACT__51AD8B76F9AA5CBC");

            entity.ToTable("CONTRACT_APPROVAL_REQUEST");

            entity.Property(e => e.contract_approval_request_decisioncomment).HasMaxLength(1000);
            entity.Property(e => e.contract_approval_request_rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.submitted_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CONTRACT_DOCUMENT>(entity =>
        {
            entity.HasKey(e => e.contract_document_id).HasName("PK__CONTRACT__05109BDA306F9EDA");

            entity.ToTable("CONTRACT_DOCUMENTS");

            entity.HasIndex(e => e.contract_template_id, "IX_ContractDocuments_TemplateId");

            entity.Property(e => e.contract_document_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.contract_document_name).HasMaxLength(1000);
            entity.Property(e => e.contract_document_type).HasDefaultValue(1);
            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.row_version)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.tenant_id)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CONTRACT_DOCUMENT_FILE>(entity =>
        {
            entity.HasKey(e => e.contract_document_file_id).HasName("PK__CONTRACT__4824D03578264A3F");

            entity.ToTable("CONTRACT_DOCUMENT_FILES");

            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CONTRACT_DOCUMENT_RELATION>(entity =>
        {
            entity.HasKey(e => e.contract_document_relation_id).HasName("PK__CONTRACT__952603CF64B30A6C");

            entity.ToTable("CONTRACT_DOCUMENT_RELATION");

            entity.Property(e => e.contract_document_relation_type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CONTRACT_ELECTRONIC_SIGNATURE>(entity =>
        {
            entity.HasKey(e => e.contract_electronic_signature_id).HasName("PK__CONTRACT__58D51B361D8EA81E");

            entity.ToTable("CONTRACT_ELECTRONIC_SIGNATURE");

            entity.Property(e => e.contract_electronic_document_hash_at_signing)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.contract_electronic_height).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.contract_electronic_ipaddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.contract_electronic_positionx).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.contract_electronic_positiony).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.contract_electronic_reject_reason).HasMaxLength(1000);
            entity.Property(e => e.contract_electronic_signatureimagestoragekey).HasMaxLength(500);
            entity.Property(e => e.contract_electronic_signaturetype)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAWN");
            entity.Property(e => e.contract_electronic_signeremail)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.contract_electronic_signername).HasMaxLength(200);
            entity.Property(e => e.contract_electronic_signerphone)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.contract_electronic_signertitle).HasMaxLength(100);
            entity.Property(e => e.contract_electronic_signertype)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.contract_electronic_useragent).HasMaxLength(1000);
            entity.Property(e => e.contract_electronic_width).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_HANDOVER_RECORD>(entity =>
        {
            entity.HasKey(e => e.contract_handover_record_id).HasName("PK__CONTRACT__913FCA7FB61D2AEA");

            entity.ToTable("CONTRACT_HANDOVER_RECORD");

            entity.Property(e => e.contract_handover_record_cancellationreason).HasMaxLength(1000);
            entity.Property(e => e.contract_handover_record_handovercode).HasMaxLength(100);
            entity.Property(e => e.contract_handover_record_location).HasMaxLength(500);
            entity.Property(e => e.contract_handover_record_locationen).HasMaxLength(500);
            entity.Property(e => e.contract_handover_record_snapshothash)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_NEGOTIATION_COMMENT>(entity =>
        {
            entity.HasKey(e => e.contract_negotiation_comment_id).HasName("PK__CONTRACT__66AD21B1F3C223B2");

            entity.ToTable("CONTRACT_NEGOTIATION_COMMENT");

            entity.Property(e => e.contract_negotiation_comment_content).HasMaxLength(4000);
            entity.Property(e => e.contract_negotiation_comment_source)
                .HasMaxLength(32)
                .IsUnicode(false)
                .HasDefaultValue("ExternalFeedback");
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_PAYMENT_MILESTONE>(entity =>
        {
            entity.HasKey(e => e.contract_payment_milestone_id).HasName("PK__CONTRACT__1D207AE1D1A7C35C");

            entity.ToTable("CONTRACT_PAYMENT_MILESTONE");

            entity.Property(e => e.contract_payment_milestone_amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.contract_payment_milestone_code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.contract_payment_milestone_conditionen).HasMaxLength(2000);
            entity.Property(e => e.contract_payment_milestone_conditionvi).HasMaxLength(2000);
            entity.Property(e => e.contract_payment_milestone_daycountmode).HasDefaultValue(1);
            entity.Property(e => e.contract_payment_milestone_dueanchor).HasDefaultValue(1);
            entity.Property(e => e.contract_payment_milestone_paymentpercent).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.contract_payment_milestone_titleen).HasMaxLength(500);
            entity.Property(e => e.contract_payment_milestone_titlevi).HasMaxLength(500);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_PHONE_VERIFY>(entity =>
        {
            entity.HasKey(e => e.contract_phone_verify_id).HasName("PK__CONTRACT__F3F950931B97D236");

            entity.ToTable("CONTRACT_PHONE_VERIFY");

            entity.Property(e => e.contract_phone_verify_phone_number_normalized)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.contract_phone_verify_phone_source)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.contract_phone_verify_reason).HasMaxLength(1000);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_RENEWAL>(entity =>
        {
            entity.HasKey(e => e.contract_renewal_id).HasName("PK__CONTRACT__DA28DA64A99807F6");

            entity.ToTable("CONTRACT_RENEWALS");

            entity.Property(e => e.approved_at_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.contract_renewal)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.contract_version_rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_SIGNED_EVIDENCE>(entity =>
        {
            entity.HasKey(e => e.contract_signed_evidence_id).HasName("PK__CONTRACT__D182826EAD6EC053");

            entity.ToTable("CONTRACT_SIGNED_EVIDENCE");

            entity.Property(e => e.contract_signed_evidence_status).HasDefaultValue(1);
            entity.Property(e => e.contract_signed_evidence_supersedereason).HasMaxLength(1000);
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.uploaded_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CONTRACT_TEMPLATE>(entity =>
        {
            entity.HasKey(e => e.contract_template_id).HasName("PK__CONTRACT__B54D32D580474A6C");

            entity.ToTable("CONTRACT_TEMPLATE");

            entity.HasIndex(e => e.contract_template_code, "UQ__CONTRACT__FF5A4CCA1EA0BAB7").IsUnique();

            entity.Property(e => e.contract_template_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.contract_template_description).HasMaxLength(2000);
            entity.Property(e => e.contract_template_language_mode).HasDefaultValue(1);
            entity.Property(e => e.contract_template_name).HasMaxLength(500);
            entity.Property(e => e.contract_template_nameen).HasMaxLength(500);
            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
        });

        modelBuilder.Entity<CONTRACT_TERM>(entity =>
        {
            entity.HasKey(e => e.contract_term_termid).HasName("PK__CONTRACT__221B7DA45BEFC080");

            entity.ToTable("CONTRACT_TERM");

            entity.Property(e => e.contract_term_bilingualcontentmode).HasDefaultValue(1);
            entity.Property(e => e.contract_term_termcode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.contract_term_termtitle).HasMaxLength(500);
            entity.Property(e => e.contract_term_termtitleen).HasMaxLength(500);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<CONTRACT_TYPE>(entity =>
        {
            entity.HasKey(e => e.contract_type_id).HasName("PK__CONTRACT__B66967DFBD999E50");

            entity.ToTable("CONTRACT_TYPES");

            entity.HasIndex(e => e.contract_type_code, "UQ__CONTRACT__E404FEBAC591B36A").IsUnique();

            entity.Property(e => e.contract_status).HasMaxLength(200);
            entity.Property(e => e.contract_type_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.contract_type_description).HasMaxLength(2000);
            entity.Property(e => e.contract_type_name).HasMaxLength(500);
            entity.Property(e => e.contract_type_nameen).HasMaxLength(500);
            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CONTRACT_VERSION>(entity =>
        {
            entity.HasKey(e => e.contract_version_id).HasName("PK__CONTRACT__EBF92F3240FF8147");

            entity.ToTable("CONTRACT_VERSION");

            entity.HasIndex(e => new { e.contract_document_id, e.contract_version_version_no }, "UX_ContractVersion_Document_VersionNo").IsUnique();

            entity.Property(e => e.contract_version_changenote).HasMaxLength(2000);
            entity.Property(e => e.contract_version_created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.contract_version_currencycode)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .IsFixedLength();
            entity.Property(e => e.contract_version_rowversion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.contract_version_snapshothash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.contract_version_subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.contract_version_totalamount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.contract_version_totaldiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.contract_version_totalvat).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<CUSTOMER>(entity =>
        {
            entity.HasKey(e => e.customer_id).HasName("PK__CUSTOMER__CD65CB8597EC383E");

            entity.ToTable("CUSTOMER");

            entity.HasIndex(e => e.customer_email, "UQ_Customer_Email_NotNull").IsUnique();

            entity.HasIndex(e => e.customer_display_name, "UQ__CUSTOMER__DB022DF2B761A468").IsUnique();

            entity.HasIndex(e => e.customer_personal_phone, "UQ__CUSTOMER__E223D5CF43FA51ED").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.customer_address).HasMaxLength(1000);
            entity.Property(e => e.customer_display_name).HasMaxLength(200);
            entity.Property(e => e.customer_email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.customer_firstname).HasMaxLength(100);
            entity.Property(e => e.customer_gender)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.customer_lastname).HasMaxLength(100);
            entity.Property(e => e.customer_middlename).HasMaxLength(100);
            entity.Property(e => e.customer_password_hash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.customer_personal_phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.customer_personal_tax_identifier)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.customer_status).HasDefaultValue(1);
        });

        modelBuilder.Entity<CUSTOMER_BUSINESS>(entity =>
        {
            entity.HasKey(e => e.customer_business_id).HasName("PK__CUSTOMER__B2CF3EC5E1B79606");

            entity.ToTable("CUSTOMER_BUSINESS");

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.customer_business_company_tax_identifier)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.customer_business_email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.customer_business_head_office_address).HasMaxLength(1000);
            entity.Property(e => e.customer_business_legal_name).HasMaxLength(1000);
            entity.Property(e => e.customer_business_office_address).HasMaxLength(1000);
            entity.Property(e => e.customer_business_phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.customer_business_representative_name).HasMaxLength(500);
            entity.Property(e => e.customer_business_type)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CUSTOMER_INTERACTION>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("CUSTOMER_INTERACTION");

            entity.Property(e => e.customer_interaction_id).ValueGeneratedOnAdd();
            entity.Property(e => e.customer_interaction_interaction_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.customer_interaction_interactionsubject).HasMaxLength(1000);
            entity.Property(e => e.customer_interaction_interactiontype)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.customer_interaction_nextfollowupdate).HasColumnType("datetime");
        });

        modelBuilder.Entity<DEPARTMENT>(entity =>
        {
            entity.HasKey(e => e.department_id).HasName("PK__DEPARTME__C2232422781A2EE2");

            entity.ToTable("DEPARTMENT");

            entity.HasIndex(e => e.department_name, "UQ__DEPARTME__226ED157B6A0C9C4").IsUnique();

            entity.HasIndex(e => e.department_code, "UQ__DEPARTME__EBC3495E93D5BDC3").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.department_code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.department_description).HasMaxLength(2000);
            entity.Property(e => e.department_name).HasMaxLength(500);
            entity.Property(e => e.department_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<DOCUMENT_TYPE>(entity =>
        {
            entity.HasKey(e => e.document_type_id).HasName("PK__DOCUMENT__69F7C2B142A83B48");

            entity.ToTable("DOCUMENT_TYPES");

            entity.Property(e => e.created_date).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.document_type_name).HasMaxLength(1000);
            entity.Property(e => e.document_type_name_en).HasMaxLength(1000);
            entity.Property(e => e.row_version)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<EMPLOYEE>(entity =>
        {
            entity.HasKey(e => e.employee_id).HasName("PK__EMPLOYEE__C52E0BA893EA187C");

            entity.ToTable("EMPLOYEE");

            entity.HasIndex(e => e.employee_email, "UQ__EMPLOYEE__0A874BCF6D354CCE").IsUnique();

            entity.HasIndex(e => e.employee_display_name, "UQ__EMPLOYEE__6A740DDF6C0D2DEC").IsUnique();

            entity.HasIndex(e => e.employee_phone, "UQ__EMPLOYEE__A10A0FAD88AF174B").IsUnique();

            entity.HasIndex(e => e.employee_code, "UQ__EMPLOYEE__B0AA7345F6B5AC8C").IsUnique();

            entity.HasIndex(e => e.employee_personal_identifier, "UQ__EMPLOYEE__D31ECC047BA803A3").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.employee_address).HasMaxLength(1000);
            entity.Property(e => e.employee_code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.employee_display_name).HasMaxLength(200);
            entity.Property(e => e.employee_email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.employee_first_name).HasMaxLength(100);
            entity.Property(e => e.employee_gender)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.employee_last_name).HasMaxLength(100);
            entity.Property(e => e.employee_middle_name).HasMaxLength(100);
            entity.Property(e => e.employee_password_hash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.employee_personal_identifier)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.employee_phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.employee_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.employee_username)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EMPLOYEE_DEPARTMENT>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("EMPLOYEE_DEPARTMENT");

            entity.HasIndex(e => new { e.employee_id, e.department_id }, "EMPLOYEE_DEPARTMENT_index_1").IsUnique();
        });

        modelBuilder.Entity<EMPLOYEE_ROLE>(entity =>
        {
            entity.HasKey(e => e.employee_role_id).HasName("PK__EMPLOYEE__AF99BE5B6931EDE6");

            entity.ToTable("EMPLOYEE_ROLE");

            entity.HasIndex(e => new { e.employee_id, e.system_role_id }, "EMPLOYEE_ROLE_index_3").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.system_member_role_assigned_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<FILE_STORAGE>(entity =>
        {
            entity.HasKey(e => e.file_storage_fileid).HasName("PK__FILE_STO__9717AEF36955EE03");

            entity.ToTable("FILE_STORAGE");

            entity.HasIndex(e => new { e.file_storage_objecttype, e.file_storage_objectid }, "IX_FileStorage_Object");

            entity.Property(e => e.file_storage_contenttype)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.file_storage_filename).HasMaxLength(300);
            entity.Property(e => e.file_storage_filepath).HasMaxLength(1000);
            entity.Property(e => e.file_storage_filetype)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.file_storage_objecttype)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.file_storage_sha256)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.file_storage_storagekey)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.file_storage_tenantcode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.file_storage_uploadeddate)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<INVOICE>(entity =>
        {
            entity.HasKey(e => e.invoice_id).HasName("PK__INVOICE__F58DFD49DD1764F1");

            entity.ToTable("INVOICE");

            entity.HasIndex(e => e.invoice_code, "UQ__INVOICE__5ED70A353ED802E8").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.invoice_billing_address).HasMaxLength(1000);
            entity.Property(e => e.invoice_billing_email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.invoice_billing_name).HasMaxLength(500);
            entity.Property(e => e.invoice_billing_phone)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.invoice_billing_tax_identifier)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.invoice_buyer_type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("INDIVIDUAL");
            entity.Property(e => e.invoice_code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.invoice_currency)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("VND");
            entity.Property(e => e.invoice_discount_amount).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.invoice_status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.invoice_subtotal).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.invoice_total_amount).HasColumnType("decimal(19, 4)");
        });

        modelBuilder.Entity<INVOICE_PROMOTION_DETAIL>(entity =>
        {
            entity.HasKey(e => e.invoice_promotion_detail_id).HasName("PK__INVOICE___5610816CFCE29728");

            entity.ToTable("INVOICE_PROMOTION_DETAIL");

            entity.Property(e => e.actual_money_deducted).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.discount_value_applied).HasColumnType("decimal(19, 4)");
        });

        modelBuilder.Entity<NOTIFICATION>(entity =>
        {
            entity.HasKey(e => e.notification_id).HasName("PK__NOTIFICA__E059842F0F9BB27C");

            entity.ToTable("NOTIFICATION");

            entity.Property(e => e.notification_actionkey)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.notification_body).HasMaxLength(1000);
            entity.Property(e => e.notification_createdatutc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.notification_eventcode)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.notification_eventkey)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.notification_modulecode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.notification_subjecttype)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.notification_title).HasMaxLength(300);
        });

        modelBuilder.Entity<NOTIFICATION_RECIPIENT>(entity =>
        {
            entity.HasKey(e => e.notification_recipient_id).HasName("PK__NOTIFICA__3F0EC20910E13CA6");

            entity.ToTable("NOTIFICATION_RECIPIENT");

            entity.Property(e => e.notification_recipient_createdatutc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<OCR_BATCH>(entity =>
        {
            entity.HasKey(e => e.ocr_patch_id).HasName("PK__OCR_BATC__3F5512F618B47202");

            entity.ToTable("OCR_BATCH");

            entity.Property(e => e.batch_status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.updated_at).HasColumnType("datetime");
        });

        modelBuilder.Entity<OCR_JOB>(entity =>
        {
            entity.HasKey(e => e.ocr_job_id).HasName("PK__OCR_JOB__AA5F821BFCC04590");

            entity.ToTable("OCR_JOB");

            entity.Property(e => e.ocr_job_avg_confidence_score).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ocr_job_processing_status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("QUEUED");
            entity.Property(e => e.scan_quality).HasMaxLength(100);
            entity.Property(e => e.scanned_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<PACKAGE_LEVEL>(entity =>
        {
            entity.HasKey(e => e.package_level_id).HasName("PK__PACKAGE___9A4639DB6832EE7A");

            entity.ToTable("PACKAGE_LEVEL");

            entity.HasIndex(e => new { e.saas_type_id, e.package_level_code }, "PACKAGE_LEVEL_index_6").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.package_level_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.package_level_description).HasMaxLength(1000);
            entity.Property(e => e.package_level_name).HasMaxLength(500);
            entity.Property(e => e.package_level_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<PACKAGE_LEVEL_CONVERSION_RATE>(entity =>
        {
            entity.HasKey(e => e.package_level_conversion_rate_id).HasName("PK__PACKAGE___FAF451BA5A996136");

            entity.ToTable("PACKAGE_LEVEL_CONVERSION_RATE");

            entity.HasIndex(e => new { e.saas_type_id, e.from_level_id, e.to_level_id }, "PACKAGE_LEVEL_CONVERSION_RATE_index_7").IsUnique();

            entity.Property(e => e.conversion_rate).HasColumnType("decimal(18, 6)");
        });

        modelBuilder.Entity<PACKAGE_LEVEL_PERMISSION>(entity =>
        {
            entity.HasKey(e => new { e.package_level_id, e.permission_id }).HasName("PK__PACKAGE___241508741EF28569");

            entity.ToTable("PACKAGE_LEVEL_PERMISSION");
        });

        modelBuilder.Entity<PAYMENT>(entity =>
        {
            entity.HasKey(e => e.payment_id).HasName("PK__PAYMENT__ED1FC9EA18BEF7FE");

            entity.ToTable("PAYMENT");

            entity.HasIndex(e => e.payment_reference, "UQ__PAYMENT__39A67FB2F2F8D7BF").IsUnique();

            entity.HasIndex(e => e.invoice_id, "UQ__PAYMENT__F58DFD48C9CBF1A8").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.payment_amount).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.payment_currency)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("VND");
            entity.Property(e => e.payment_method)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.payment_provider)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.payment_provider_transaction_id)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.payment_reference)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.payment_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SUCCESS");
        });

        modelBuilder.Entity<PLAN>(entity =>
        {
            entity.HasKey(e => e.plan_id).HasName("PK__PLAN__BE9F8F1D7584A84A");

            entity.ToTable("PLAN");

            entity.HasIndex(e => new { e.saas_type_id, e.plan_code }, "PLAN_index_8").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.plan_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.plan_name).HasMaxLength(150);
            entity.Property(e => e.plan_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
        });

        modelBuilder.Entity<PLAN_VERSION>(entity =>
        {
            entity.HasKey(e => e.plan_version_id).HasName("PK__PLAN_VER__58158D5407554D53");

            entity.ToTable("PLAN_VERSION");

            entity.HasIndex(e => new { e.plan_id, e.plan_version_number }, "PLAN_VERSION_index_9").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.plan_version_currency)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("VND");
            entity.Property(e => e.plan_version_price).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.plan_version_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
        });

        modelBuilder.Entity<PRODUCT>(entity =>
        {
            entity.HasKey(e => e.product_id).HasName("PK__PRODUCTS__47027DF5F456F82C");

            entity.ToTable("PRODUCTS");

            entity.Property(e => e.product_code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.product_created_at).HasColumnType("datetime");
            entity.Property(e => e.product_deployment).HasMaxLength(4000);
            entity.Property(e => e.product_document)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.product_iconclass)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.product_largeimage)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.product_metadescription).HasMaxLength(2000);
            entity.Property(e => e.product_metakeyword).HasMaxLength(2000);
            entity.Property(e => e.product_name).HasMaxLength(500);
            entity.Property(e => e.product_nameen).HasMaxLength(500);
            entity.Property(e => e.product_others).HasMaxLength(4000);
            entity.Property(e => e.product_rewrite)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.product_shortdesc).HasMaxLength(2000);
            entity.Property(e => e.product_shortname).HasMaxLength(100);
            entity.Property(e => e.product_slogan).HasMaxLength(400);
            entity.Property(e => e.product_smallimage)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.product_suggested_price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.product_tags).HasMaxLength(500);
            entity.Property(e => e.product_titlebrowser).HasMaxLength(500);
            entity.Property(e => e.product_unitname).HasMaxLength(100);
            entity.Property(e => e.product_unitnameen).HasMaxLength(100);
        });

        modelBuilder.Entity<PROMOTION>(entity =>
        {
            entity.HasKey(e => e.promotion_id).HasName("PK__PROMOTIO__2CB9556B72EBBEA8");

            entity.ToTable("PROMOTION");

            entity.HasIndex(e => e.promotion_code, "UQ__PROMOTIO__88875505D58B0392").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.max_usage_per_customer).HasDefaultValue(1);
            entity.Property(e => e.promotion_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.promotion_description).HasMaxLength(1000);
            entity.Property(e => e.promotion_discount_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.promotion_discount_value).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.promotion_max_discount_amount).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.promotion_name).HasMaxLength(500);
            entity.Property(e => e.promotion_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<PROMOTION_CUSTOMER_USAGE>(entity =>
        {
            entity.HasKey(e => e.customer_usage_id).HasName("PK__PROMOTIO__114EC9B948BA860D");

            entity.ToTable("PROMOTION_CUSTOMER_USAGE");

            entity.HasIndex(e => new { e.promotion_id, e.customer_id }, "PROMOTION_CUSTOMER_USAGE_index_10").IsUnique();

            entity.Property(e => e.last_used_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<PROMOTION_PLAN>(entity =>
        {
            entity.HasKey(e => new { e.promotion_id, e.plan_id }).HasName("PK__PROMOTIO__E750AD9A8FF2169A");

            entity.ToTable("PROMOTION_PLAN");
        });

        modelBuilder.Entity<PROMOTION_SAAS_TYPE>(entity =>
        {
            entity.HasKey(e => new { e.promotion_id, e.saas_type_id }).HasName("PK__PROMOTIO__E333A6A8232894D0");

            entity.ToTable("PROMOTION_SAAS_TYPE");
        });

        modelBuilder.Entity<SAAS_PERMISSION>(entity =>
        {
            entity.HasKey(e => e.permission_id).HasName("PK__SAAS_PER__E5331AFA90C0B76D");

            entity.ToTable("SAAS_PERMISSION");

            entity.HasIndex(e => e.permission_code, "UQ__SAAS_PER__A98A808E95EA0F70").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.permission_code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.permission_description).HasMaxLength(1000);
            entity.Property(e => e.permission_name).HasMaxLength(300);
        });

        modelBuilder.Entity<SAAS_PERMISSION_DEPENDENCY>(entity =>
        {
            entity.HasKey(e => e.dependency_id).HasName("PK__SAAS_PER__952A1872B6F48BB7");

            entity.ToTable("SAAS_PERMISSION_DEPENDENCY");

            entity.HasIndex(e => new { e.permission_id, e.depends_on_permission_id }, "SAAS_PERMISSION_DEPENDENCY_index_5").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<SAAS_PERMISSION_GROUP>(entity =>
        {
            entity.HasKey(e => e.permission_group_id).HasName("PK__SAAS_PER__EE3284C6ED23894C");

            entity.ToTable("SAAS_PERMISSION_GROUP");

            entity.HasIndex(e => new { e.saas_type_id, e.group_code }, "SAAS_PERMISSION_GROUP_index_4").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.group_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.group_description).HasMaxLength(1000);
            entity.Property(e => e.group_icon)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.group_name).HasMaxLength(300);
        });

        modelBuilder.Entity<SAAS_TYPE>(entity =>
        {
            entity.HasKey(e => e.saas_type_id).HasName("PK__SAAS_TYP__F8AF3C3DAEADF5D4");

            entity.ToTable("SAAS_TYPE");

            entity.HasIndex(e => e.saas_type_code, "UQ__SAAS_TYP__2C12F920CDB5F0AD").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.saas_type_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.saas_type_description).HasMaxLength(2000);
            entity.Property(e => e.saas_type_name).HasMaxLength(1000);
            entity.Property(e => e.saas_type_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<SERVICE>(entity =>
        {
            entity.HasKey(e => e.service_id).HasName("PK__SERVICES__3E0DB8AFF23CD48F");

            entity.ToTable("SERVICES");

            entity.Property(e => e.service_datecreated)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.service_datemodified).HasColumnType("datetime");
            entity.Property(e => e.service_imageicon)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.service_metadescription).HasMaxLength(2000);
            entity.Property(e => e.service_metakeyword).HasMaxLength(2000);
            entity.Property(e => e.service_name).HasMaxLength(2000);
            entity.Property(e => e.service_nameen).HasMaxLength(500);
            entity.Property(e => e.service_others).HasMaxLength(4000);
            entity.Property(e => e.service_price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.service_rewrite)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.service_titlebrowser).HasMaxLength(500);
            entity.Property(e => e.service_unitname).HasMaxLength(100);
            entity.Property(e => e.service_unitnameen).HasMaxLength(100);
        });

        modelBuilder.Entity<SERVICE_TYPE>(entity =>
        {
            entity.HasKey(e => e.servicetype_id).HasName("PK__SERVICE___5F6CB0FAF55D8AFB");

            entity.ToTable("SERVICE_TYPES");

            entity.Property(e => e.servicetype_name).HasMaxLength(200);
        });

        modelBuilder.Entity<SYSTEM_PERMISSION>(entity =>
        {
            entity.HasKey(e => e.system_permission_id).HasName("PK__SYSTEM_P__F3CB9804852CE60E");

            entity.ToTable("SYSTEM_PERMISSION");

            entity.HasIndex(e => e.system_permission_code, "UQ__SYSTEM_P__8CA6D51642B4C86D").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.system_permission_action)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.system_permission_code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.system_permission_description).HasMaxLength(2000);
            entity.Property(e => e.system_permission_name).HasMaxLength(300);
            entity.Property(e => e.system_permission_resource)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SYSTEM_PERMISSION_DEPENDENCY>(entity =>
        {
            entity.HasKey(e => e.system_permission_dependency_id).HasName("PK__SYSTEM_P__27981FBC186C742E");

            entity.ToTable("SYSTEM_PERMISSION_DEPENDENCY");

            entity.HasIndex(e => new { e.system_permission_id, e.depends_on_system_permission_id }, "SYSTEM_PERMISSION_DEPENDENCY_index_2").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<SYSTEM_PERMISSION_GROUP>(entity =>
        {
            entity.HasKey(e => e.system_permission_group_id).HasName("PK__SYSTEM_P__6D5CBFFA65161237");

            entity.ToTable("SYSTEM_PERMISSION_GROUP");

            entity.HasIndex(e => e.system_permission_group_code, "UQ__SYSTEM_P__2860628D678567FD").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.system_permission_group_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.system_permission_group_description).HasMaxLength(2000);
            entity.Property(e => e.system_permission_group_name).HasMaxLength(300);
        });

        modelBuilder.Entity<SYSTEM_ROLE>(entity =>
        {
            entity.HasKey(e => e.system_role_id).HasName("PK__SYSTEM_R__FC704761852622B9");

            entity.ToTable("SYSTEM_ROLE");

            entity.HasIndex(e => e.system_role_code, "UQ__SYSTEM_R__D218DF4498CAB93A").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.system_role_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.system_role_description).HasMaxLength(500);
            entity.Property(e => e.system_role_name).HasMaxLength(200);
            entity.Property(e => e.system_role_scope)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SYSTEM");
            entity.Property(e => e.system_role_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<SYSTEM_ROLE_PERMISSION>(entity =>
        {
            entity.HasKey(e => new { e.system_role_id, e.system_permission_id }).HasName("PK__SYSTEM_R__A34CFEE12C8AB391");

            entity.ToTable("SYSTEM_ROLE_PERMISSION");

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<TENANT>(entity =>
        {
            entity.HasKey(e => e.tenant_id).HasName("PK__TENANT__D6F29F3E2BC2AAF3");

            entity.ToTable("TENANT");

            entity.HasIndex(e => e.tenant_code, "UQ__TENANT__6568FE0993262370").IsUnique();

            entity.HasIndex(e => e.tenant_subdomain, "UQ__TENANT__9357B5AA366CCF8E").IsUnique();

            entity.Property(e => e.activated_at).HasColumnType("datetime");
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.suspended_at).HasColumnType("datetime");
            entity.Property(e => e.tenant_code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.tenant_name).HasMaxLength(500);
            entity.Property(e => e.tenant_status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.tenant_subdomain)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TENANT_DATABASE>(entity =>
        {
            entity.HasKey(e => e.tenant_database_id).HasName("PK__TENANT_D__F437F1962C0E3A77");

            entity.ToTable("TENANT_DATABASE");

            entity.HasIndex(e => e.tenant_id, "UQ__TENANT_D__D6F29F3F8E4A931A").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.tenant_database_db_name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.tenant_database_host)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.tenant_database_password_encrypted)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.tenant_database_provider)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.tenant_database_provisioning_status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("FRESH");
            entity.Property(e => e.tenant_database_user)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.tenant_schema_version)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TICKET_ATTACHMENT>(entity =>
        {
            entity.HasKey(e => e.ticket_attachment_id).HasName("PK__TICKET_A__FF9BA4FEEFBA3F35");

            entity.ToTable("TICKET_ATTACHMENTS");

            entity.Property(e => e.file_name_1)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.file_name_2)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.file_name_3)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.mime_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.uploaded_at).HasColumnType("datetime");
        });

        modelBuilder.Entity<TICKET_HISTORY>(entity =>
        {
            entity.HasKey(e => e.ticket_history_id).HasName("PK__TICKET_H__0BE2545600BCA91D");

            entity.ToTable("TICKET_HISTORY");

            entity.Property(e => e.changed_at).HasColumnType("datetime");
            entity.Property(e => e.new_status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.notes).HasMaxLength(500);
            entity.Property(e => e.old_status)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TICKET_MESSAGE>(entity =>
        {
            entity.HasKey(e => e.ticket_message_id).HasName("PK__TICKET_M__90FF0B653A82232F");

            entity.ToTable("TICKET_MESSAGE");

            entity.Property(e => e.created_at).HasColumnType("datetime");
            entity.Property(e => e.ticket_message_type)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TICKET_SUPPORT>(entity =>
        {
            entity.HasKey(e => e.ticket_support_id).HasName("PK__TICKET_S__30F865D0D48C2891");

            entity.ToTable("TICKET_SUPPORT");

            entity.Property(e => e.created_at).HasColumnType("datetime");
            entity.Property(e => e.ticket_closed_at).HasColumnType("datetime");
            entity.Property(e => e.ticket_code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ticket_priority)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ticket_resolved_at).HasColumnType("datetime");
            entity.Property(e => e.ticket_status)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ticket_title).HasMaxLength(1000);
            entity.Property(e => e.updated_at).HasColumnType("datetime");
        });

        modelBuilder.Entity<TICKET_SUPPORT_EMPLOYEE>(entity =>
        {
            entity.HasKey(e => e.ticket_support_employee_id).HasName("PK__TICKET_S__8C600BDFD0DA80BE");

            entity.ToTable("TICKET_SUPPORT_EMPLOYEE");

            entity.Property(e => e.ticket_support_employee_assigned_at).HasColumnType("datetime");
            entity.Property(e => e.ticket_support_employee_assignment_role)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ticket_support_employee_unassigned_at).HasColumnType("datetime");
        });

        modelBuilder.Entity<TICKET_TYPE>(entity =>
        {
            entity.HasKey(e => e.ticket_type_id).HasName("PK__TICKET_T__9531B7D1FEC27295");

            entity.ToTable("TICKET_TYPES");

            entity.Property(e => e.ticket_name).HasMaxLength(500);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
