using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ContractManagement.Infrastructure.Persistence.Central.Saas;
/// <summary>
/// Gọi trong CentralDbContext.OnModelCreating:  modelBuilder.ApplySaasModel();
/// Tên bảng/cột giữ đúng ScripDB.sql (snake_case, bảng viết hoa).
/// </summary>
public static class SaasModelConfiguration
{
    public static ModelBuilder ApplySaasModel(this ModelBuilder b)
    {
        b.ApplyConfiguration(new CustomerConfig());
        b.ApplyConfiguration(new CustomerBusinessConfig());
        b.ApplyConfiguration(new SaasTypeConfig());
        b.ApplyConfiguration(new PlanConfig());
        b.ApplyConfiguration(new PlanVersionConfig());
        b.ApplyConfiguration(new SubscriptionConfig());
        b.ApplyConfiguration(new PromotionConfig());
        b.ApplyConfiguration(new InvoiceConfig());
        b.Entity<InvoiceEmailOutbox>(e =>
        {
            e.ToTable("INVOICE_EMAIL_OUTBOX");
            e.HasKey(x => x.OutboxId);
            e.Property(x => x.Kind).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => new { x.InvoiceId, x.Kind }).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.ApplyConfiguration(new PaymentConfig());
        b.ApplyConfiguration(new InvoicePromotionDetailConfig());
        b.ApplyConfiguration(new SubscriptionPromotionDetailConfig());
        return b;
    }
    // ---------- helpers ----------
    private static PropertyBuilder<T> Ascii<T>(this PropertyBuilder<T> p, int len) => p.HasMaxLength(len).IsUnicode(false);
    private static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> p) => p.HasColumnType("decimal(19,4)");
    private static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> p) => p.HasColumnType("decimal(19,4)");
    private static PropertyBuilder<DateTime> Dt(this PropertyBuilder<DateTime> p) => p.HasColumnType("datetime");
    private static PropertyBuilder<DateTime?> Dt(this PropertyBuilder<DateTime?> p) => p.HasColumnType("datetime");
    private static PropertyBuilder<T> Cur<T>(this PropertyBuilder<T> p) => p.HasMaxLength(3).IsUnicode(false).IsFixedLength();
    private static void Audit<T>(EntityTypeBuilder<T> e) where T : class
    {
        e.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        e.Property<DateTime?>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime");
        e.Property<long?>("CreatedBy").HasColumnName("created_by");
        e.Property<long?>("UpdatedBy").HasColumnName("updated_by");
    }
    // ---------- CUSTOMER ----------
    private sealed class CustomerConfig : IEntityTypeConfiguration<SaasCustomer>
    {
        public void Configure(EntityTypeBuilder<SaasCustomer> e)
        {
            e.ToTable("CUSTOMER");
            e.HasKey(x => x.CustomerId).HasName("PK_CUSTOMER");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.CustomerEmail).HasColumnName("customer_email").Ascii(255).IsRequired();
            e.HasIndex(x => x.CustomerEmail).IsUnique().HasDatabaseName("UX_CUSTOMER_customer_email");
            e.Property(x => x.CustomerPhone).HasColumnName("customer_phone").Ascii(15);
            e.Property(x => x.CustomerHandPhone).HasColumnName("customer_hand_phone").Ascii(15);
            e.Property(x => x.CustomerPasswordHash).HasColumnName("customer_password_hash").Ascii(255); // NULL được
            e.Property(x => x.CustomerDisplayName).HasColumnName("customer_display_name").HasMaxLength(100); // NVARCHAR để lưu tên tiếng Việt (script là VARCHAR)
            e.Property(x => x.CustomerType).HasColumnName("customer_type").Ascii(20).HasDefaultValue("CUSTOMER");
            e.Property(x => x.CustomerStatus).HasColumnName("customer_status").Ascii(20).HasDefaultValue("ACTIVE");
            e.Property(x => x.CustomerEmailVerifiedAt).HasColumnName("customer_email_verified_at").Dt();
            e.Property(x => x.CustomerHandPhoneVerifiedAt).HasColumnName("customer_hand_phone_verified_at").Dt();
            e.Property(x => x.CustomerLastLoginAt).HasColumnName("customer_last_login_at").Dt();
            e.Property(x => x.CustomerDob).HasColumnName("customer_dob").HasColumnType("date");
            e.Property(x => x.CustomerAddress).HasColumnName("customer_address").HasMaxLength(500);
            Audit(e);
        }
    }
    // ---------- CUSTOMER_BUSINESS ----------
    private sealed class CustomerBusinessConfig : IEntityTypeConfiguration<SaasCustomerBusiness>
    {
        public void Configure(EntityTypeBuilder<SaasCustomerBusiness> e)
        {
            e.ToTable("CUSTOMER_BUSINESS");
            e.HasKey(x => x.CustomerId).HasName("PK_CUSTOMER_BUSINESS");
            e.Property(x => x.CustomerId).HasColumnName("customer_id").ValueGeneratedNever();
            e.Property(x => x.LegalName).HasColumnName("customer_business_legal_name").HasMaxLength(1000).IsRequired();
            e.Property(x => x.CompanyTaxIdentifier).HasColumnName("customer_business_company_tax_identifier").Ascii(20).IsRequired();
            e.Property(x => x.Phone).HasColumnName("customer_business_phone").Ascii(15);
            e.Property(x => x.BusinessEmail).HasColumnName("customer_business_business_email").Ascii(255);
            e.Property(x => x.OfficeAddress).HasColumnName("customer_business_business_office_address").HasMaxLength(1000);
            e.Property(x => x.HeadOfficeAddress).HasColumnName("customer_business_business_head_office_address").HasMaxLength(1000);
            e.Property(x => x.BusinessType).HasColumnName("customer_business_business_type").Ascii(100);
            e.Property(x => x.RepresentativeName).HasColumnName("customer_business_representative_name").HasMaxLength(255);
            e.Property(x => x.OperationStartDate).HasColumnName("customer_business_operation_start_date").HasColumnType("date");
            Audit(e);
            e.HasOne(x => x.Customer).WithOne(c => c.Business)
             .HasForeignKey<SaasCustomerBusiness>(x => x.CustomerId)
             .HasConstraintName("FK_CUSTOMER_BUSINESS_CUSTOMER").OnDelete(DeleteBehavior.Cascade);
        }
    }
    // ---------- SAAS_TYPE ----------
    private sealed class SaasTypeConfig : IEntityTypeConfiguration<SaasType>
    {
        public void Configure(EntityTypeBuilder<SaasType> e)
        {
            e.ToTable("SAAS_TYPE");
            e.HasKey(x => x.SaasTypeId).HasName("PK_SAAS_TYPE");
            e.Property(x => x.SaasTypeId).HasColumnName("saas_type_id");
            e.Property(x => x.SaasTypeCode).HasColumnName("saas_type_code").Ascii(50).IsRequired();
            e.HasIndex(x => x.SaasTypeCode).IsUnique().HasDatabaseName("UX_SAAS_TYPE_code");
            e.Property(x => x.SaasTypeName).HasColumnName("saas_type_name").HasMaxLength(150).IsRequired();
            e.Property(x => x.SaasTypeDescription).HasColumnName("saas_type_description").HasMaxLength(500);
            e.Property(x => x.SaasTypeStatus).HasColumnName("saas_type_status").Ascii(20).HasDefaultValue("ACTIVE");
            Audit(e);
        }
    }
    // ---------- PLAN ----------
    private sealed class PlanConfig : IEntityTypeConfiguration<SaasPlan>
    {
        public void Configure(EntityTypeBuilder<SaasPlan> e)
        {
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => x.PlanCode).IsUnique();
            e.ToTable("PLAN");   // EF tự bọc [PLAN] – không lỗi từ khóa như khi chạy script tay
            e.HasKey(x => x.PlanId).HasName("PK_PLAN");
            e.Property(x => x.PlanId).HasColumnName("plan_id");
            e.Property(x => x.SaasTypeId).HasColumnName("saas_type_id");
            e.Property(x => x.PlanCode).HasColumnName("plan_code").Ascii(50).IsRequired();
            e.Property(x => x.PlanName).HasColumnName("plan_name").HasMaxLength(150).IsRequired();
            e.Property(x => x.PlanDescription).HasColumnName("plan_description").HasMaxLength(500);
            e.Property(x => x.PlanStatus).HasColumnName("plan_status").Ascii(20).HasDefaultValue("ACTIVE");
            e.Property(x => x.IsPaid).HasColumnName("is_paid");
            e.Property(x => x.AllowsSupportTickets).HasColumnName("allows_support_tickets");
            e.Property(x => x.MaxContracts).HasColumnName("max_contracts").HasDefaultValue(10);
            e.Property(x => x.MaxScanPages).HasColumnName("max_scan_pages").HasDefaultValue(20);
            e.Property(x => x.MaxUsers).HasColumnName("max_users").HasDefaultValue(2);
            Audit(e);
            e.HasOne(x => x.SaasType).WithMany(t => t.Plans).HasForeignKey(x => x.SaasTypeId)
             .HasConstraintName("FK_PLAN_SAAS_TYPE").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- PLAN_VERSION ----------
    private sealed class PlanVersionConfig : IEntityTypeConfiguration<SaasPlanVersion>
    {
        public void Configure(EntityTypeBuilder<SaasPlanVersion> e)
        {
            e.ToTable("PLAN_VERSION");
            e.HasKey(x => x.PlanVersionId).HasName("PK_PLAN_VERSION");
            e.Property(x => x.PlanVersionId).HasColumnName("plan_version_id");
            e.Property(x => x.PlanId).HasColumnName("plan_id");
            e.Property(x => x.VersionNumber).HasColumnName("plan_version_version_number");
            e.HasIndex(x => new { x.PlanId, x.VersionNumber }).IsUnique().HasDatabaseName("UX_PLAN_VERSION_Number");
            e.Property(x => x.PurchaseType).HasColumnName("plan_version_purchase_type").Ascii(20).HasDefaultValue("BUYMANY");
            e.Property(x => x.DurationMonths).HasColumnName("plan_version_duration_months");
            e.Property(x => x.DurationDays).HasColumnName("plan_version_duration_days");
            e.Property(x => x.Price).HasColumnName("plan_version_price").Money().HasDefaultValue(0m);
            e.Property(x => x.Currency).HasColumnName("plan_version_currency").Cur().HasDefaultValue("VND");
            e.Property(x => x.BillingCycle).HasColumnName("plan_version_billing_cycle").Ascii(20).HasDefaultValue("MONTHLY");
            e.Property(x => x.EffectiveFrom).HasColumnName("plan_version_effective_from").Dt().HasDefaultValueSql("GETDATE()");
            e.Property(x => x.EffectiveTo).HasColumnName("plan_version_effective_to").Dt();
            e.Property(x => x.Status).HasColumnName("plan_version_status").Ascii(20).HasDefaultValue("ACTIVE");
            Audit(e);
            e.HasOne(x => x.Plan).WithMany(p => p.Versions).HasForeignKey(x => x.PlanId)
             .HasConstraintName("FK_PLAN_VERSION_PLAN").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- SUBSCRIPTION ----------
    private sealed class SubscriptionConfig : IEntityTypeConfiguration<SaasSubscription>
    {
        public void Configure(EntityTypeBuilder<SaasSubscription> e)
        {
            e.ToTable("SUBSCRIPTION");
            e.Property(x => x.ProvisioningLeaseId).IsConcurrencyToken();
            e.HasKey(x => x.SubscriptionId).HasName("PK_SUBSCRIPTION");
            e.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");               // NULL được
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.PlanVersionId).HasColumnName("plan_version_id");
            e.Property(x => x.Status).HasColumnName("subscription_status").Ascii(20).HasDefaultValue("PENDING");
            e.Property(x => x.StartAt).HasColumnName("subscription_start_at").Dt();
            e.Property(x => x.EndAt).HasColumnName("subscription_end_at").Dt();
            e.Property(x => x.PriceSnapshot).HasColumnName("subscription_price_snapshot").Money().HasDefaultValue(0m);
            e.Property(x => x.Currency).HasColumnName("subscription_currency").Cur().HasDefaultValue("VND");
            e.Property(x => x.BillingCycle).HasColumnName("subscription_billing_cycle").Ascii(20).HasDefaultValue("MONTHLY");
            e.Property(x => x.AutoRenew).HasColumnName("subscription_auto_renew");
            e.Property(x => x.IsPaid).HasColumnName("is_paid");
            e.Property(x => x.RequestedTenantCode).HasColumnName("requested_tenant_code").Ascii(50).IsRequired();
            e.Property(x => x.RequestedTenantName).HasColumnName("requested_tenant_name").HasMaxLength(500).IsRequired();
            e.Property(x => x.PublicId).HasColumnName("subscription_public_id").HasDefaultValueSql("NEWID()");
            e.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_SUBSCRIPTION_public_id");
            e.Property(x => x.ProvisioningAttemptCount).HasColumnName("provisioning_attempt_count").HasDefaultValue(0);
            Audit(e);
            e.HasIndex(x => x.TenantId).HasDatabaseName("IX_SUBSCRIPTION_Tenant");
            e.HasIndex(x => x.CustomerId).HasDatabaseName("IX_SUBSCRIPTION_Customer");
            e.HasIndex(x => new { x.Status, x.IsPaid }).HasDatabaseName("IX_SUBSCRIPTION_Status");
            e.HasIndex(x => x.RequestedTenantCode).HasDatabaseName("IX_SUBSCRIPTION_RequestedTenantCode");
            e.HasOne(x => x.Customer).WithMany(c => c.Subscriptions).HasForeignKey(x => x.CustomerId)
             .HasConstraintName("FK_SUBSCRIPTION_CUSTOMER").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PlanVersion).WithMany(v => v.Subscriptions).HasForeignKey(x => x.PlanVersionId)
             .HasConstraintName("FK_SUBSCRIPTION_PLAN_VERSION").OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .HasConstraintName("FK_SUBSCRIPTION_Tenants").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- PROMOTION ----------
    private sealed class PromotionConfig : IEntityTypeConfiguration<SaasPromotion>
    {
        public void Configure(EntityTypeBuilder<SaasPromotion> e)
        {
            e.ToTable("PROMOTION");
            e.HasKey(x => x.PromotionId).HasName("PK_PROMOTION");
            e.Property(x => x.PromotionId).HasColumnName("promotion_id");
            e.Property(x => x.PromotionCode).HasColumnName("promotion_code").Ascii(50).IsRequired();
            e.HasIndex(x => x.PromotionCode).IsUnique().HasDatabaseName("UX_PROMOTION_code");
            e.Property(x => x.PromotionName).HasColumnName("promotion_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.PromotionDescription).HasColumnName("promotion_description").HasMaxLength(500);
            e.Property(x => x.DiscountType).HasColumnName("promotion_discount_type").Ascii(20).IsRequired();
            e.Property(x => x.DiscountValue).HasColumnName("promotion_discount_value").Money();
            e.Property(x => x.MaxDiscountAmount).HasColumnName("promotion_max_discount_amount").Money();
            e.Property(x => x.StartAt).HasColumnName("promotion_start_at").Dt();
            e.Property(x => x.EndAt).HasColumnName("promotion_end_at").Dt();
            e.Property(x => x.UsageLimit).HasColumnName("promotion_usage_limit");
            e.Property(x => x.UsageCount).HasColumnName("promotion_usage_count").HasDefaultValue(0).IsConcurrencyToken();
            e.Property(x => x.Status).HasColumnName("promotion_status").Ascii(20).HasDefaultValue("ACTIVE");
            Audit(e);
        }
    }
    // ---------- SAAS_INVOICE ----------
    private sealed class InvoiceConfig : IEntityTypeConfiguration<SaasInvoice>
    {
        public void Configure(EntityTypeBuilder<SaasInvoice> e)
        {
            e.ToTable("SAAS_INVOICE");
            e.Property(x => x.PublicId).HasDefaultValueSql("NEWID()");
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.PlanNameSnapshot).HasMaxLength(150);
            e.Property(x => x.BuyerNameSnapshot).HasMaxLength(255);
            e.HasKey(x => x.InvoiceId).HasName("PK_SAAS_INVOICE");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.InvoiceCode).HasColumnName("invoice_code").Ascii(100).IsRequired();
            e.HasIndex(x => x.InvoiceCode).IsUnique().HasDatabaseName("UX_SAAS_INVOICE_code");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            e.Property(x => x.BillingName).HasColumnName("invoice_billing_name").HasMaxLength(255).IsRequired();
            e.Property(x => x.BillingTaxIdentifier).HasColumnName("invoice_billing_tax_identifier").Ascii(100);
            e.Property(x => x.BillingAddress).HasColumnName("invoice_billing_address").HasMaxLength(500);
            e.Property(x => x.BillingEmail).HasColumnName("invoice_billing_email").Ascii(255);
            e.Property(x => x.BillingPhone).HasColumnName("invoice_billing_phone").Ascii(30);
            e.Property(x => x.Subtotal).HasColumnName("invoice_subtotal").Money();
            e.Property(x => x.DiscountAmount).HasColumnName("invoice_discount_amount").Money().HasDefaultValue(0m);
            e.Property(x => x.TotalAmount).HasColumnName("invoice_total_amount").Money();
            e.Property(x => x.Currency).HasColumnName("invoice_currency").Cur().HasDefaultValue("VND");
            e.Property(x => x.Status).IsConcurrencyToken().HasColumnName("invoice_status").Ascii(30).HasDefaultValue("DRAFT");
            e.Property(x => x.IssuedAt).HasColumnName("invoice_issued_at").Dt();
            e.Property(x => x.DueAt).HasColumnName("invoice_due_at").Dt();
            e.Property(x => x.PaidAt).HasColumnName("invoice_paid_at").Dt();
            e.Property(x => x.PayosOrderCode).HasColumnName("payos_order_code");
            e.HasIndex(x => x.PayosOrderCode).IsUnique().HasFilter("[payos_order_code] IS NOT NULL").HasDatabaseName("UX_SAAS_INVOICE_payos_order_code");
            e.Property(x => x.PayosPaymentLinkId).HasColumnName("payos_payment_link_id").Ascii(100);
            e.Property(x => x.PayosCheckoutUrl).HasColumnName("payos_checkout_url").HasMaxLength(1000);
            Audit(e);
            e.HasOne(x => x.Customer).WithMany(c => c.Invoices).HasForeignKey(x => x.CustomerId)
             .HasConstraintName("FK_SAAS_INVOICE_CUSTOMER").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subscription).WithMany(s => s.Invoices).HasForeignKey(x => x.SubscriptionId)
             .HasConstraintName("FK_SAAS_INVOICE_SUBSCRIPTION").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- SAAS_PAYMENT ----------
    private sealed class PaymentConfig : IEntityTypeConfiguration<SaasPayment>
    {
        public void Configure(EntityTypeBuilder<SaasPayment> e)
        {
            e.ToTable("SAAS_PAYMENT");
            e.HasKey(x => x.PaymentId).HasName("PK_SAAS_PAYMENT");
            e.Property(x => x.PaymentId).HasColumnName("payment_id");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.PaymentReference).HasColumnName("payment_reference").Ascii(100).IsRequired();
            e.HasIndex(x => x.PaymentReference).IsUnique().HasDatabaseName("UX_SAAS_PAYMENT_ref");
            e.Property(x => x.PaymentMethod).HasColumnName("payment_method").Ascii(30).IsRequired();
            e.Property(x => x.PaymentProvider).HasColumnName("payment_provider").Ascii(100);
            e.Property(x => x.ProviderTransactionId).HasColumnName("payment_provider_transaction_id").Ascii(255);
            e.Property(x => x.Amount).HasColumnName("payment_amount").Money();
            e.Property(x => x.Currency).HasColumnName("payment_currency").Cur().HasDefaultValue("VND");
            e.Property(x => x.Status).HasColumnName("payment_status").Ascii(30).HasDefaultValue("PENDING");
            e.Property(x => x.PaidAt).HasColumnName("payment_paid_at").Dt();
            e.Property(x => x.FailureReason).HasColumnName("payment_failure_reason").Ascii(500);
            Audit(e);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId)
             .HasConstraintName("FK_SAAS_PAYMENT_INVOICE").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- INVOICE_PROMOTION_DETAIL ----------
    private sealed class InvoicePromotionDetailConfig : IEntityTypeConfiguration<SaasInvoicePromotionDetail>
    {
        public void Configure(EntityTypeBuilder<SaasInvoicePromotionDetail> e)
        {
            e.ToTable("INVOICE_PROMOTION_DETAIL");
            e.HasKey(x => x.InvoicePromotionDetailId).HasName("PK_INVOICE_PROMOTION_DETAIL");
            e.Property(x => x.InvoicePromotionDetailId).HasColumnName("invoice_promotion_detail_id");
            e.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            e.Property(x => x.PromotionId).HasColumnName("promotion_id");
            e.Property(x => x.DiscountTypeApplied).HasColumnName("discount_type_applied").Ascii(20).IsRequired();
            e.Property(x => x.DiscountValueApplied).HasColumnName("discount_value_applied").Money();
            e.Property(x => x.ActualMoneyDeducted).HasColumnName("actual_money_deducted").Money();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").Dt().HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Invoice).WithMany(i => i.PromotionDetails).HasForeignKey(x => x.InvoiceId)
             .HasConstraintName("FK_INVOICE_PROMOTION_DETAIL_INVOICE").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId)
             .HasConstraintName("FK_INVOICE_PROMOTION_DETAIL_PROMOTION").OnDelete(DeleteBehavior.Restrict);
        }
    }
    // ---------- SUBSCRIPTION_PROMOTION_DETAIL ----------
    private sealed class SubscriptionPromotionDetailConfig : IEntityTypeConfiguration<SaasSubscriptionPromotionDetail>
    {
        public void Configure(EntityTypeBuilder<SaasSubscriptionPromotionDetail> e)
        {
            e.ToTable("SUBSCRIPTION_PROMOTION_DETAIL");
            e.HasKey(x => x.SubscriptionPromotionDetailId).HasName("PK_SUBSCRIPTION_PROMOTION_DETAIL");
            e.Property(x => x.SubscriptionPromotionDetailId).HasColumnName("subscription_promotion_detail_id");
            e.Property(x => x.PromotionId).HasColumnName("promotion_id");
            e.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            e.Property(x => x.DiscountTypeApplied).HasColumnName("discount_type_applied").Ascii(20).IsRequired();
            e.Property(x => x.DiscountValueApplied).HasColumnName("discount_value_applied").Money();
            e.Property(x => x.ActualMoneyDeducted).HasColumnName("actual_money_deducted").Money();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").Dt().HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId)
             .HasConstraintName("FK_SUBSCRIPTION_PROMOTION_DETAIL_PROMOTION").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subscription).WithMany(s => s.PromotionDetails).HasForeignKey(x => x.SubscriptionId)
             .HasConstraintName("FK_SUBSCRIPTION_PROMOTION_DETAIL_SUBSCRIPTION").OnDelete(DeleteBehavior.Restrict);
        }
    }
}
