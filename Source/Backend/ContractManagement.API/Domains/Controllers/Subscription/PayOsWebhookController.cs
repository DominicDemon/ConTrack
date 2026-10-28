using ContractManagement.API.Domains.Services.Subscription;
using ContractManagement.Attributes;
using Microsoft.AspNetCore.Mvc;
using PayOS.Exceptions;
using PayOS.Models.Webhooks;

namespace ContractManagement.Domains.Controllers.Subscription;

/// <summary>
/// Webhook PayOS (không session, không tenant). Chữ ký sai → 400; còn lại luôn 200 để PayOS không gửi lại.
/// Không tạo tenant trong request này; job nền sẽ kích hoạt.
/// </summary>
[ApiController]
[Route("api/payments/payos/webhook")]
[AllowWithoutTenant]
public sealed class PayOsWebhookController : ControllerBase
{
    private readonly SubscriptionFulfillmentService _fulfillment;

    public PayOsWebhookController(SubscriptionFulfillmentService fulfillment)
    {
        _fulfillment = fulfillment;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] Webhook webhook, CancellationToken cancellationToken)
    {
        try
        {
            await _fulfillment.HandleWebhookAsync(webhook, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidSignatureException or WebhookException)
        {
            return BadRequest(new { success = false });
        }
        return Ok(new { success = true });
    }
}
