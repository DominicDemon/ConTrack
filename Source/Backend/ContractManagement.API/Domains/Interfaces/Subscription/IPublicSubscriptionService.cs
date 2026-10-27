using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.DTOs.Responses.Subscription;

namespace ContractManagement.API.Domains.Interfaces.Subscription;

public interface IPublicSubscriptionService
{
    Task<IReadOnlyList<PublicPlanResponse>> GetPlansAsync(CancellationToken cancellationToken = default);

    Task<TenantCodeCheckResponse> CheckTenantCodeAsync(string? code, CancellationToken cancellationToken = default);

    Task<VoucherPreviewResponse> PreviewVoucherAsync(VoucherPreviewRequest request, CancellationToken cancellationToken = default);

    Task<SubscriptionCheckoutResponse> CheckoutAsync(SubscriptionCheckoutRequest request, CancellationToken cancellationToken = default);

    Task<SubscriptionStatusResponse> GetStatusAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<SubscriptionStatusResponse> CancelAsync(Guid publicId, CancellationToken cancellationToken = default);
}
