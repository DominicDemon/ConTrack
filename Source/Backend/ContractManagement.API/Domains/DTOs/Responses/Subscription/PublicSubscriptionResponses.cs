namespace ContractManagement.API.Domains.DTOs.Responses.Subscription;

public sealed record PublicPlanResponse(
    long PlanId,
    string PlanCode,
    string PlanName,
    string? PlanDescription,
    bool IsPaid,
    bool AllowsSupportTickets,
    int MaxUsers,
    int MaxContracts,
    int MaxScanPages,
    IReadOnlyList<PublicPlanVersionResponse> Versions);

public sealed record PublicPlanVersionResponse(
    long PlanVersionId,
    int VersionNumber,
    decimal Price,
    string Currency,
    string BillingCycle,
    int? DurationDays, int? DurationMonths = null);

public sealed record TenantCodeCheckResponse(
    string TenantCode,
    bool Available,
    string? ErrorCode,
    string? Message);

public sealed record VoucherPreviewResponse(
    long PlanVersionId,
    string? VoucherCode,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string Currency);

/// <summary>CheckoutUrl cÃ³ giÃ¡ trá»‹ khi cáº§n chuyá»ƒn sang PayOS; null vá»›i Ä‘Æ¡n 0Ä‘ (Ä‘i tháº³ng tá»›i mÃ n káº¿t quáº£).</summary>
public sealed record SubscriptionCheckoutResponse(
    Guid PublicId,
    string InvoiceCode,
    decimal Total,
    string Currency,
    string? CheckoutUrl);

public sealed record SubscriptionStatusResponse(
    Guid PublicId,
    string Status,
    bool IsPaid,
    string Message,
    string TenantCode,
    string TenantName,
    string InvoiceCode,
    decimal Total,
    string Currency,
    string? CheckoutUrl, string? ReceiptPath = null);
