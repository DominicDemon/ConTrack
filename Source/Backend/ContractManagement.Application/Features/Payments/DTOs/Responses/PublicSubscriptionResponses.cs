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
    int? DurationDays);

public sealed record TenantCodeCheckResponse(
    string TenantCode,
    bool Available,
    string? ErrorCode,
    string? Message);

public sealed record EmailCheckResponse(
    string Email,
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

/// <summary>CheckoutUrl có giá trị khi cần chuyển sang PayOS; null với đơn 0đ (đi thẳng tới màn kết quả).</summary>
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
    string? CheckoutUrl);
