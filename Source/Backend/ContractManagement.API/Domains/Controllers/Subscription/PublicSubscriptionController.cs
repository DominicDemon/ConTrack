using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.DTOs.Responses.Subscription;
using ContractManagement.API.Domains.Interfaces.Subscription;
using ContractManagement.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.Domains.Controllers.Subscription;

/// <summary>
/// API công khai (không cần đăng nhập, không cần X-Tenant-Code) cho luồng tự đăng ký gói.
/// Lỗi nghiệp vụ trả về dạng { code, message } với mã ổn định cho FE.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowWithoutTenant]
public sealed class PublicSubscriptionController : ControllerBase
{
    private readonly IPublicSubscriptionService _service;

    public PublicSubscriptionController(IPublicSubscriptionService service)
    {
        _service = service;
    }

    /// <summary>Màn (1): các gói ACTIVE kèm phiên bản giá đang hiệu lực, gói dùng thử đứng đầu.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<PublicPlanResponse>>.Ok(await _service.GetPlansAsync(cancellationToken)));

    /// <summary>Màn (2): mã tổ chức hợp lệ và chưa bị dùng/giữ chỗ. Luôn 200, xem trường available.</summary>
    [HttpGet("tenant-code/check")]
    public async Task<IActionResult> CheckTenantCode([FromQuery] string? code, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TenantCodeCheckResponse>.Ok(await _service.CheckTenantCodeAsync(code, cancellationToken)));

    /// <summary>Màn (3): tính subtotal/discount/total với voucher. Không trừ lượt voucher.</summary>
    [HttpPost("vouchers/preview")]
    public async Task<IActionResult> PreviewVoucher([FromBody] VoucherPreviewRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VoucherPreviewResponse>.Ok(await _service.PreviewVoucherAsync(request, cancellationToken)));

    /// <summary>
    /// Màn (3) → (4): tạo đơn. checkoutUrl khác null thì chuyển trình duyệt sang PayOS;
    /// null (gói 0đ) thì đi thẳng tới màn (5) với publicId.
    /// </summary>
    [HttpPost("subscriptions/checkout")]
    public async Task<IActionResult> Checkout([FromBody] SubscriptionCheckoutRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SubscriptionCheckoutResponse>.Ok(await _service.CheckoutAsync(request, cancellationToken)));

    /// <summary>Màn (5): FE poll mỗi 3–5 giây.</summary>
    [HttpGet("subscriptions/{publicId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid publicId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<SubscriptionStatusResponse>.Ok(await _service.GetStatusAsync(publicId, cancellationToken)));
    }

    /// <summary>Màn (5) khi PayOS redirect về với cancel=true: hủy nếu đơn vẫn chưa thanh toán.</summary>
    [HttpPost("subscriptions/{publicId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid publicId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SubscriptionStatusResponse>.Ok(await _service.CancelAsync(publicId, cancellationToken)));
}
