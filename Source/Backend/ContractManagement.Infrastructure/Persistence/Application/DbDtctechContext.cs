using ContractManagement.Application.Common.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

/// <summary>
/// Unified Application & Tenant DbContext (Clean Code).
/// - Hiện tại kết nối trực tiếp tới MasterDB (dùng chung).
/// - Khi một Tenant có Database riêng trong tương lai, DI sẽ tự động trỏ DbContext này sang Tenant Database đó.
/// - Toàn bộ cấu hình Fluent API được tách gọn theo từng miền trong thư mục Configurations/.
/// </summary>
public partial class DbDtctechContext : DbContext, IApplicationDbContext
{
    public DbDtctechContext()
    {
    }

    public DbDtctechContext(DbContextOptions<DbDtctechContext> options)
        : base(options)
    {
    }

    #region 1. Core Organization, Catalog & CRM
    public virtual DbSet<TblDepartment> TblDepartments { get; set; } = null!;
    public virtual DbSet<TblEmployee> TblEmployees { get; set; } = null!;
    public virtual DbSet<TblCustomer> TblCustomers { get; set; } = null!;
    public virtual DbSet<TblCustomerInteraction> TblCustomerInteractions { get; set; } = null!;
    public virtual DbSet<TblCategory> TblCategories { get; set; } = null!;
    public virtual DbSet<TblProduct> TblProducts { get; set; } = null!;
    public virtual DbSet<TblService> TblServices { get; set; } = null!;
    public virtual DbSet<TblServiceType> TblServiceTypes { get; set; } = null!;
    public virtual DbSet<TblTenantLegalProfile> TblTenantLegalProfiles { get; set; } = null!;
    #endregion

    #region 2. Contracts, Versions, Templates & Placeholders
    public virtual DbSet<TblContract> TblContracts { get; set; } = null!;
    public virtual DbSet<TblContractVersion> TblContractVersions { get; set; } = null!;
    public virtual DbSet<TblContractItem> TblContractItems { get; set; } = null!;
    public virtual DbSet<TblContractTerm> TblContractTerms { get; set; } = null!;
    public virtual DbSet<TblContractLegalBasis> TblContractLegalBases { get; set; } = null!;
    public virtual DbSet<TblContractAppendix> TblContractAppendices { get; set; } = null!;
    public virtual DbSet<TblContractAppendixTerm> TblContractAppendixTerms { get; set; } = null!;
    public virtual DbSet<TblContractAttachment> TblContractAttachments { get; set; } = null!;
    public virtual DbSet<TblContractSignedEvidence> TblContractSignedEvidences { get; set; } = null!;

    public virtual DbSet<TblContractVersionLegalSnapshot> TblContractVersionLegalSnapshots { get; set; } = null!;
    public virtual DbSet<TblContractVersionPartySnapshot> TblContractVersionPartySnapshots { get; set; } = null!;
    public virtual DbSet<TblContractVersionPaymentMilestoneSnapshot> TblContractVersionPaymentMilestoneSnapshots { get; set; } = null!;

    public virtual DbSet<TblContractTemplate> TblContractTemplates { get; set; } = null!;
    public virtual DbSet<TblContractTemplateVersion> TblContractTemplateVersions { get; set; } = null!;
    public virtual DbSet<TblContractTemplateItemTableColumnLayout> TblContractTemplateItemTableColumnLayouts { get; set; } = null!;
    public virtual DbSet<TblContractTemplateField> TblContractTemplateFields { get; set; } = null!;
    public virtual DbSet<TblContractTemplateTerm> TblContractTemplateTerms { get; set; } = null!;
    public virtual DbSet<TblContractTemplateAppendix> TblContractTemplateAppendices { get; set; } = null!;
    public virtual DbSet<TblContractTemplateAppendixTerm> TblContractTemplateAppendixTerms { get; set; } = null!;
    public virtual DbSet<TblContractTemplatePaymentMilestone> TblContractTemplatePaymentMilestones { get; set; } = null!;
    public virtual DbSet<TblContractTemplateLegalBasis> TblContractTemplateLegalBases { get; set; } = null!;

    public virtual DbSet<TblContractPlaceholderDefinition> TblContractPlaceholderDefinitions { get; set; } = null!;
    public virtual DbSet<TblContractPlaceholderAudit> TblContractPlaceholderAudits { get; set; } = null!;
    public virtual DbSet<TblContractVersionPlaceholderValue> TblContractVersionPlaceholderValues { get; set; } = null!;
    #endregion

    #region 3. Approvals, Negotiation, Handover & Acceptance
    public virtual DbSet<TblContractApprovalRequest> TblContractApprovalRequests { get; set; } = null!;
    public virtual DbSet<TblApprovalHistory> TblApprovalHistories { get; set; } = null!;
    public virtual DbSet<TblApprovalWorkflow> TblApprovalWorkflows { get; set; } = null!;
    public virtual DbSet<TblContractNegotiationComment> TblContractNegotiationComments { get; set; } = null!;
    public virtual DbSet<TblContractNegotiationCommentEvent> TblContractNegotiationCommentEvents { get; set; } = null!;

    public virtual DbSet<TblContractAcceptanceEvidence> TblContractAcceptanceEvidences { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceRecord> TblContractAcceptanceRecords { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceReference> TblContractAcceptanceReferences { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceParty> TblContractAcceptanceParties { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceSection> TblContractAcceptanceSections { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceMilestone> TblContractAcceptanceMilestones { get; set; } = null!;
    public virtual DbSet<TblContractAcceptanceScopeAllocation> TblContractAcceptanceScopeAllocations => Set<TblContractAcceptanceScopeAllocation>();

    public virtual DbSet<TblContractHandoverBaseline> TblContractHandoverBaselines => Set<TblContractHandoverBaseline>();
    public virtual DbSet<TblContractHandoverScopeItem> TblContractHandoverScopeItems => Set<TblContractHandoverScopeItem>();
    public virtual DbSet<TblContractHandoverRecord> TblContractHandoverRecords => Set<TblContractHandoverRecord>();
    public virtual DbSet<TblContractHandoverLine> TblContractHandoverLines => Set<TblContractHandoverLine>();
    public virtual DbSet<TblContractHandoverParty> TblContractHandoverParties => Set<TblContractHandoverParty>();
    public virtual DbSet<TblContractHandoverReference> TblContractHandoverReferences => Set<TblContractHandoverReference>();
    public virtual DbSet<TblContractHandoverSection> TblContractHandoverSections => Set<TblContractHandoverSection>();
    public virtual DbSet<TblContractHandoverEvidence> TblContractHandoverEvidences => Set<TblContractHandoverEvidence>();
    public virtual DbSet<TblContractHandoverPlaceholderValue> TblContractHandoverPlaceholderValues => Set<TblContractHandoverPlaceholderValue>();
    #endregion

    #region 4. Billing, Quotations, Orders & Payments
    public virtual DbSet<TblQuotation> TblQuotations { get; set; } = null!;
    public virtual DbSet<TblQuotationDetail> TblQuotationDetails { get; set; } = null!;
    public virtual DbSet<TblOrder> TblOrders { get; set; } = null!;
    public virtual DbSet<TblOrderDetail> TblOrderDetails { get; set; } = null!;
    public virtual DbSet<TblDeliveryOrder> TblDeliveryOrders { get; set; } = null!;
    public virtual DbSet<TblDeliveryDetail> TblDeliveryDetails { get; set; } = null!;
    public virtual DbSet<TblInvoice> TblInvoices { get; set; } = null!;
    public virtual DbSet<TblPayment> TblPayments { get; set; } = null!;
    public virtual DbSet<TblContractPaymentLedger> TblContractPaymentLedgers { get; set; } = null!;
    public virtual DbSet<TblContractPaymentMilestone> TblContractPaymentMilestones { get; set; } = null!;
    #endregion

    #region 5. Customer Access, Notifications, Files, Recovery & Audits
    public virtual DbSet<TblContractCustomerVerificationPhone> TblContractCustomerVerificationPhones { get; set; } = null!;
    public virtual DbSet<TblContractCustomerAccessLink> TblContractCustomerAccessLinks { get; set; } = null!;
    public virtual DbSet<TblContractCustomerOtpChallenge> TblContractCustomerOtpChallenges { get; set; } = null!;
    public virtual DbSet<TblContractCustomerAccessSession> TblContractCustomerAccessSessions { get; set; } = null!;
    public virtual DbSet<TblContractCustomerOtpDeliveryOutbox> TblContractCustomerOtpDeliveryOutbox { get; set; } = null!;

    public virtual DbSet<TblNotification> TblNotifications { get; set; } = null!;
    public virtual DbSet<TblNotificationRecipient> TblNotificationRecipients { get; set; } = null!;
    public virtual DbSet<TblNotificationDispatchOutbox> TblNotificationDispatchOutbox { get; set; } = null!;
    public virtual DbSet<TblFileStorage> TblFileStorages { get; set; } = null!;

    public DbSet<AccountRecoveryChallenge> RecoveryChallenges => Set<AccountRecoveryChallenge>();
    public DbSet<AccountRecoveryEmailOutbox> RecoveryEmailOutbox => Set<AccountRecoveryEmailOutbox>();

    public virtual DbSet<TblContractAudit> TblContractAudits { get; set; } = null!;
    public virtual DbSet<TblContractAuditValue> TblContractAuditValues { get; set; } = null!;
    public virtual DbSet<TblContractTemplateAudit> TblContractTemplateAudits { get; set; } = null!;
    public virtual DbSet<TblContractTemplateAuditValue> TblContractTemplateAuditValues { get; set; } = null!;
    public virtual DbSet<TblAuthorizationAudit> TblAuthorizationAudits { get; set; } = null!;
    #endregion

    #region 6. Support Tickets & MasterDB Table Aliases
    public virtual DbSet<TICKET_SUPPORT> TICKET_SUPPORT { get; set; } = null!;
    public virtual DbSet<TICKET_MESSAGE> TICKET_MESSAGE { get; set; } = null!;
    public virtual DbSet<TICKET_ATTACHMENTS> TICKET_ATTACHMENTS { get; set; } = null!;
    public virtual DbSet<TICKET_HISTORY> TICKET_HISTORY { get; set; } = null!;
    public virtual DbSet<TICKET_SUPPORT_EMPLOYEE> TICKET_SUPPORT_EMPLOYEE { get; set; } = null!;
    public virtual DbSet<TICKET_TYPES> TICKET_TYPES { get; set; } = null!;

    public virtual DbSet<DEPARTMENT> DEPARTMENT { get => TblDepartments; set => TblDepartments = value; }
    public virtual DbSet<EMPLOYEE> EMPLOYEE { get => TblEmployees; set => TblEmployees = value; }
    public virtual DbSet<CUSTOMER> CUSTOMER { get => TblCustomers; set => TblCustomers = value; }
    public virtual DbSet<CATEGORIES> CATEGORIES { get => TblCategories; set => TblCategories = value; }
    public virtual DbSet<PRODUCTS> PRODUCTS { get => TblProducts; set => TblProducts = value; }
    public virtual DbSet<SERVICES> SERVICES { get => TblServices; set => TblServices = value; }
    public virtual DbSet<SERVICE_TYPES> SERVICE_TYPES { get => TblServiceTypes; set => TblServiceTypes = value; }
    public virtual DbSet<CONTRACTS> CONTRACTS { get => TblContracts; set => TblContracts = value; }
    public virtual DbSet<INVOICE> INVOICE { get => TblInvoices; set => TblInvoices = value; }
    public virtual DbSet<PAYMENT> PAYMENT { get => TblPayments; set => TblPayments = value; }
    public virtual DbSet<NOTIFICATION> NOTIFICATION { get => TblNotifications; set => TblNotifications = value; }
    public virtual DbSet<FILE_STORAGE> FILE_STORAGE { get => TblFileStorages; set => TblFileStorages = value; }
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Core Domain Entities
        CoreEntitiesModelConfiguration.Configure(modelBuilder);

        // 2. Extended Contract Modules (Catalog Units, Handover, Placeholders, LegalBases, Snapshots, Bilingual)
        ContractExtendedModelConfiguration.ConfigureAll(modelBuilder);

        // 3. Appendix & Acceptance Modules
        AppendixAcceptanceModelConfiguration.Configure(modelBuilder);

        // 4. Support Tickets & MasterDB Table Mappings
        SupportTicketsModelConfiguration.ConfigureSupportTickets(modelBuilder);

        // 5. Account Recovery Outbox & Challenge
        AccountRecoveryMapping.Configure(
            modelBuilder,
            "tbl_AccountRecoveryChallenge",
            "tbl_AccountRecoveryEmailOutbox",
            "EmployeeId");

        OnModelCreatingPartial(modelBuilder);
    }

    public static void ConfigureSupportTickets(ModelBuilder modelBuilder) =>
        SupportTicketsModelConfiguration.ConfigureSupportTickets(modelBuilder);

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

public sealed class ApplicationDbContext(DbContextOptions<DbDtctechContext> options)
    : DbDtctechContext(options)
{
}
