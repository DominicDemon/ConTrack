
// Central DB – SaaS subscription entities (mapped from ScripDB.sql, phần 1.2 – 1.4)
// Namespace đặt cạnh CentralDbContext. Đổi lại nếu project đặt entity Central ở thư mục khác.
namespace ContractManagement.Infrastructure.Persistence.Central.Saas;
/// <summary>CUSTOMER – người mua gói (Business Owner). Không dùng để đăng nhập ở phiên bản này.</summary>
public class SaasCustomer
{
    public long CustomerId { get; set; }                       // customer_id
    public string CustomerEmail { get; set; } = null!;         // customer_email (UQ)
    public string? CustomerPhone { get; set; }                 // customer_phone
    public string? CustomerHandPhone { get; set; }             // customer_hand_phone
    public string? CustomerPasswordHash { get; set; }          // customer_password_hash  (ĐỔI: NULL được)
    public string? CustomerDisplayName { get; set; }           // customer_display_name  (= "Tên đầy đủ của bạn")
    public string CustomerType { get; set; } = "CUSTOMER";     // customer_type
    public string CustomerStatus { get; set; } = "ACTIVE";     // customer_status
    public DateTime? CustomerEmailVerifiedAt { get; set; }
    public DateTime? CustomerHandPhoneVerifiedAt { get; set; }
    public DateTime? CustomerLastLoginAt { get; set; }
    public DateTime? CustomerDob { get; set; }
    public string? CustomerAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasCustomerBusiness? Business { get; set; }
    public ICollection<SaasSubscription> Subscriptions { get; set; } = new List<SaasSubscription>();
    public ICollection<SaasInvoice> Invoices { get; set; } = new List<SaasInvoice>();
}
/// <summary>CUSTOMER_BUSINESS – hồ sơ pháp nhân (1 : 0..1). Luồng đăng ký hiện tại KHÔNG ghi bảng này (form không có mã số thuế).</summary>
public class SaasCustomerBusiness
{
    public long CustomerId { get; set; }                       // PK + FK
    public string LegalName { get; set; } = null!;             // customer_business_legal_name
    public string CompanyTaxIdentifier { get; set; } = null!;  // customer_business_company_tax_identifier
    public string? Phone { get; set; }
    public string? BusinessEmail { get; set; }
    public string? OfficeAddress { get; set; }
    public string? HeadOfficeAddress { get; set; }
    public string? BusinessType { get; set; }
    public string? RepresentativeName { get; set; }
    public DateTime? OperationStartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasCustomer Customer { get; set; } = null!;
}
/// <summary>SAAS_TYPE – loại giải pháp.</summary>
public class SaasType
{
    public long SaasTypeId { get; set; }
    public string SaasTypeCode { get; set; } = null!;          // UQ
    public string SaasTypeName { get; set; } = null!;
    public string? SaasTypeDescription { get; set; }
    public string SaasTypeStatus { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public ICollection<SaasPlan> Plans { get; set; } = new List<SaasPlan>();
}
/// <summary>PLAN – gói cước (Free/Trial hoặc trả phí) do System Admin cấu hình.</summary>
public class SaasPlan
{
    public long PlanId { get; set; }
    public long SaasTypeId { get; set; }
    public string PlanCode { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public string? PlanDescription { get; set; }
    public string PlanStatus { get; set; } = "ACTIVE";
    public bool IsPaid { get; set; }                           // 0 = Free/Trial, 1 = trả phí
    public bool AllowsSupportTickets { get; set; }
    public int MaxContracts { get; set; } = 10;
    public int MaxScanPages { get; set; } = 20;
    public int MaxUsers { get; set; } = 2;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasType SaasType { get; set; } = null!;
    public ICollection<SaasPlanVersion> Versions { get; set; } = new List<SaasPlanVersion>();
}
/// <summary>PLAN_VERSION – bảng giá & chu kỳ của gói.</summary>
public class SaasPlanVersion
{
    public long PlanVersionId { get; set; }
    public long PlanId { get; set; }
    public int VersionNumber { get; set; }                     // plan_version_version_number (UQ cùng plan_id)
    public string PurchaseType { get; set; } = "BUYMANY";      // plan_version_purchase_type
    public int? DurationDays { get; set; }                     // số ngày dùng (vd trial 14 ngày)
    public decimal Price { get; set; }                         // DECIMAL(19,4)
    public string Currency { get; set; } = "VND";              // CHAR(3)
    public string BillingCycle { get; set; } = "MONTHLY";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasPlan Plan { get; set; } = null!;
    public ICollection<SaasSubscription> Subscriptions { get; set; } = new List<SaasSubscription>();
}
/// <summary>SUBSCRIPTION – đơn thuê bao. Được tạo ở bước "Confirm cart"; tenant được gán sau khi thanh toán.</summary>
public class SaasSubscription
{
    public long SubscriptionId { get; set; }
    public int? TenantId { get; set; }                         // tenant_id  (ĐỔI: NULL được, gán khi provision xong)
    public long? CustomerId { get; set; }
    public long PlanVersionId { get; set; }
    public string Status { get; set; } = "PENDING";            // PENDING, ACTIVE, PAYMENT_FAILED, PROVISIONING_FAILED, EXPIRED, SUSPENDED, CANCELLED
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public decimal PriceSnapshot { get; set; }
    public string Currency { get; set; } = "VND";
    public string BillingCycle { get; set; } = "MONTHLY";
    public bool AutoRenew { get; set; }
    public bool IsPaid { get; set; }
    public string RequestedTenantCode { get; set; } = null!;   // BỔ SUNG: "Mã tổ chức" người dùng nhập
    public string RequestedTenantName { get; set; } = null!;   // BỔ SUNG: "Tên tổ chức"
    public Guid PublicId { get; set; }                         // BỔ SUNG: mã khó đoán cho URL trạng thái public
    public int ProvisioningAttemptCount { get; set; }          // BỔ SUNG: giới hạn số lần retry tạo tenant
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasCustomer? Customer { get; set; }
    public SaasPlanVersion PlanVersion { get; set; } = null!;
    public ICollection<SaasInvoice> Invoices { get; set; } = new List<SaasInvoice>();
    public ICollection<SaasSubscriptionPromotionDetail> PromotionDetails { get; set; } = new List<SaasSubscriptionPromotionDetail>();
    // Navigation tới Tenants: thêm nếu entity Tenant hiện có cho phép (xem file cấu hình).
}
/// <summary>PROMOTION – mã voucher.</summary>
public class SaasPromotion
{
    public long PromotionId { get; set; }
    public string PromotionCode { get; set; } = null!;         // UQ – mã người dùng nhập ở trang cart
    public string PromotionName { get; set; } = null!;
    public string? PromotionDescription { get; set; }
    public string DiscountType { get; set; } = null!;          // PERCENTAGE, FIXED_AMOUNT
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
}
/// <summary>SAAS_INVOICE – hóa đơn của subscription.</summary>
public class SaasInvoice
{
    public long InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;           // UQ, vd INV-202610-000123
    public long CustomerId { get; set; }
    public long? SubscriptionId { get; set; }
    public string BillingName { get; set; } = null!;           // = Tên tổ chức
    public string? BillingTaxIdentifier { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingEmail { get; set; }
    public string? BillingPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "DRAFT";             // DRAFT, UNPAID, PAID, CANCELLED, EXPIRED
    public DateTime? IssuedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public long? PayosOrderCode { get; set; }                  // BỔ SUNG: orderCode gửi PayOS (UQ khi khác NULL)
    public string? PayosPaymentLinkId { get; set; }            // BỔ SUNG
    public string? PayosCheckoutUrl { get; set; }              // BỔ SUNG
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasCustomer Customer { get; set; } = null!;
    public SaasSubscription? Subscription { get; set; }
    public ICollection<SaasPayment> Payments { get; set; } = new List<SaasPayment>();
    public ICollection<SaasInvoicePromotionDetail> PromotionDetails { get; set; } = new List<SaasInvoicePromotionDetail>();
}
/// <summary>SAAS_PAYMENT – giao dịch thanh toán (ghi từ webhook PayOS / job đối soát).</summary>
public class SaasPayment
{
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public string PaymentReference { get; set; } = null!;      // UQ – khóa chống ghi trùng webhook
    public string PaymentMethod { get; set; } = null!;         // PAYOS, FREE
    public string? PaymentProvider { get; set; }               // "PAYOS"
    public string? ProviderTransactionId { get; set; }         // reference giao dịch ngân hàng
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "PENDING";            // PENDING, SUCCESS, FAILED, CANCELLED, AMOUNT_MISMATCH
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public SaasInvoice Invoice { get; set; } = null!;
}
/// <summary>INVOICE_PROMOTION_DETAIL – voucher áp dụng cho hóa đơn.</summary>
public class SaasInvoicePromotionDetail
{
    public long InvoicePromotionDetailId { get; set; }
    public long InvoiceId { get; set; }
    public long PromotionId { get; set; }
    public string DiscountTypeApplied { get; set; } = null!;
    public decimal DiscountValueApplied { get; set; }
    public decimal ActualMoneyDeducted { get; set; }
    public DateTime CreatedAt { get; set; }
    public SaasInvoice Invoice { get; set; } = null!;
    public SaasPromotion Promotion { get; set; } = null!;
}
/// <summary>SUBSCRIPTION_PROMOTION_DETAIL – voucher áp dụng cho subscription.</summary>
public class SaasSubscriptionPromotionDetail
{
    public long SubscriptionPromotionDetailId { get; set; }
    public long PromotionId { get; set; }
    public long SubscriptionId { get; set; }
    public string DiscountTypeApplied { get; set; } = null!;
    public decimal DiscountValueApplied { get; set; }
    public decimal ActualMoneyDeducted { get; set; }
    public DateTime CreatedAt { get; set; }
    public SaasPromotion Promotion { get; set; } = null!;
    public SaasSubscription Subscription { get; set; } = null!;
}
