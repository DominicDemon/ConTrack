using ContractManagement.API.Domains.Services.Subscription;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.Domains.Controllers.Subscription;

[ApiController]
[Route("api/billing/invoices")]
[SessionAuthorize]
public sealed class InvoicesController(InvoiceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.ListAsync(HttpContext.Session.GetInt32("TenantId")!.Value,
            HttpContext.Session.GetInt32("EmployeeId")!.Value, page, ct));
    }

    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> Get(Guid publicId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.GetAsync(publicId, HttpContext.Session.GetInt32("TenantId")!.Value,
            HttpContext.Session.GetInt32("EmployeeId")!.Value, ct);
        return result is null ? NotFound(new { code = "InvoiceUnavailable", message = "Không có quyền xem hoặc chứng từ chưa sẵn sàng." }) : Ok(result);
    }
}

[ApiController]
[Route("api/public/invoices")]
[AllowWithoutTenant]
public sealed class InvoiceReadinessController(InvoiceService service) : ControllerBase
{
    [HttpGet("{publicId:guid}/readiness")]
    public async Task<IActionResult> Get(Guid publicId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.GetReadinessAsync(publicId, ct);
        return result is null ? NotFound(new { code = "InvoiceUnavailable" }) : Ok(result);
    }
}
