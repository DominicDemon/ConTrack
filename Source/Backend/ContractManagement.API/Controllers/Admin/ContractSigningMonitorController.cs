using ContractManagement.Attributes;
using ContractManagement.Filter;
using ContractManagement.Application.Features.Internal;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Admin;

[ApiController]
[Route("api/admin/contracts/signing-monitor")]
[AllowWithoutTenant]
[SystemAdminAuthorize]
public sealed class ContractSigningMonitorController(IInternalRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SigningMonitorEntry>>> List(
        [FromQuery] string? search, [FromQuery] int? status, CancellationToken cancellationToken) =>
        Ok(await repository.SigningMonitor(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), status, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SigningMonitorDetail>> Detail(long id, CancellationToken cancellationToken) =>
        await repository.SigningMonitorDetail(id, cancellationToken) is { } detail ? Ok(detail) : NotFound();
}
