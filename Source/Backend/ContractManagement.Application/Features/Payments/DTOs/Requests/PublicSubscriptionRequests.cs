namespace ContractManagement.API.Domains.DTOs.Requests.Subscription;

public sealed class VoucherPreviewRequest
{
    public long PlanVersionId { get; set; }

    public string? VoucherCode { get; set; }
}

public sealed class SubscriptionCheckoutRequest
{
    public long PlanVersionId { get; set; }

    public string? TenantCode { get; set; }

    public string? TenantName { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? VoucherCode { get; set; }
}
