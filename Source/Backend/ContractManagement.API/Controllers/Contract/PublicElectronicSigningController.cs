using ContractManagement.API.Common.Responses;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Attributes;
using ContractManagement.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.Domains.Controllers.Contract;

[ApiController]
[Route("public/electronic-signing/{tenantCode}/{linkToken}")]
[PublicCustomerAccess]
[ServiceFilter(typeof(ElectronicSigningEnabledFilter))]
public sealed class PublicElectronicSigningController : ControllerBase
{
    private readonly IElectronicSigningService _service;

    public PublicElectronicSigningController(IElectronicSigningService service)
    {
        _service = service;
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(
        string tenantCode,
        string linkToken,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        var result = await _service.RequestCustomerOtpAsync(linkToken, cancellationToken);
        return Accepted(ApiResponse<RequestElectronicSigningOtpResponse>.Ok(
            result, "If the link is valid, an OTP has been queued for delivery."));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp(
        string tenantCode,
        string linkToken,
        [FromBody] VerifyElectronicSigningOtpRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        try
        {
            await _service.VerifyCustomerOtpAsync(linkToken, request, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(ApiResponse<object>.Fail("OTP is invalid or expired."));
        }
    }

    [HttpGet("document")]
    public async Task<IActionResult> GetDocument(
        string tenantCode,
        string linkToken,
        [FromQuery] string challengeId,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        try
        {
            var document = await _service.GetCustomerSigningPdfAsync(
                linkToken, challengeId, cancellationToken);
            return File(document.Content, "application/pdf", document.FileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Customer verification is required."));
        }
    }

    [HttpPost("sign")]
    public async Task<IActionResult> Sign(
        string tenantCode,
        string linkToken,
        [FromBody] CustomerSignElectronicContractRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        var signed = await _service.SignAsCustomerAsync(linkToken, request, cancellationToken);
        return File(signed.Content, "application/pdf", signed.FileName);
    }

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
