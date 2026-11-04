using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Responses.Admin;
using ContractManagement.API.Domains.Interfaces.Admin;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContractManagement.API.Domains.Controllers.Admin;

[ApiController]
[AllowWithoutTenant]
public sealed class SystemHealthController : ControllerBase
{
    private readonly ISystemHealthService _systemHealthService;
    private readonly HealthCheckService _healthChecks;

    public SystemHealthController(ISystemHealthService systemHealthService, HealthCheckService healthChecks)
    {
        _systemHealthService = systemHealthService;
        _healthChecks = healthChecks;
    }

    [HttpGet("/health/live")]
    public IActionResult Live() => Ok(new { status = "Healthy" });

    [HttpGet("/health/master")]
    public async Task<IActionResult> Master(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("master"), cancellationToken);
        var ready = report.Entries.Count > 0 && report.Status == HealthStatus.Healthy;
        var response = new { status = ready ? "Ready" : "Unavailable" };
        return ready ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var ready = await _systemHealthService.IsReadyAsync(cancellationToken);
        var response = new { status = ready ? "Ready" : "Unavailable" };
        return ready
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpGet("/api/admin/system-health")]
    [SystemAdminAuthorize]
    [ProducesResponseType(typeof(SystemHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthorizationErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SystemHealthResponse>> Details(
        CancellationToken cancellationToken) =>
        Ok(await _systemHealthService.GetDetailedAsync(cancellationToken));
}
