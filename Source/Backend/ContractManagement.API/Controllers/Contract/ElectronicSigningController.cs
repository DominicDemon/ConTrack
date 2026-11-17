using ContractManagement.API.Common.Responses;
using ContractManagement.API.Common.Security;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Filter;
using ContractManagement.API.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Contract;

[ApiController]
[Route("api/contracts/electronic-signing")]
[SessionAuthorize]
[ServiceFilter(typeof(ElectronicSigningEnabledFilter))]
public sealed class ElectronicSigningController : ControllerBase
{
    private readonly IElectronicSigningService _service;

    public ElectronicSigningController(IElectronicSigningService service)
    {
        _service = service;
    }

    [HttpGet("contracts/{contractId:int}/versions/{versionId:int}/status")]
    public async Task<IActionResult> GetSessionStatus(
        int contractId,
        int versionId,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        var result = await _service.GetSessionStatusAsync(
            contractId, versionId, employeeId, cancellationToken);
        return Ok(ApiResponse<ElectronicSigningSessionResponse?>.Ok(result));
    }

    [HttpGet("contracts/{contractId:int}/versions/{versionId:int}/final-document")]
    public async Task<IActionResult> GetFinalDocument(
        int contractId,
        int versionId,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        var document = await _service.GetFinalSignedPdfAsync(
            contractId, versionId, employeeId, cancellationToken);
        return File(document.Content, "application/pdf", document.FileName);
    }

    [HttpGet("director/queue")]
    [SessionAuthorize(RbacPermissions.ContractElectronicSigningDirector)]
    public async Task<IActionResult> GetDirectorQueue(CancellationToken cancellationToken)
    {
        var queue = await _service.GetDirectorQueueAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DirectorSigningQueueItem>>.Ok(queue));
    }

    [HttpGet("{sessionId:long}/director/document")]
    [SessionAuthorize(RbacPermissions.ContractElectronicSigningDirector)]
    public async Task<IActionResult> GetDirectorDocument(
        long sessionId,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        var document = await _service.GetDirectorPdfAsync(sessionId, employeeId, cancellationToken);
        return File(document.Content, "application/pdf", document.FileName);
    }

    [HttpPost]
    [SessionAuthorize(RbacPermissions.ContractSigningInitiate)]
    [ProducesResponseType(typeof(ApiResponse<ElectronicSigningSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(
        [FromBody] StartElectronicSigningRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        var result = await _service.StartAsync(request, employeeId, cancellationToken);
        return Ok(ApiResponse<ElectronicSigningSessionResponse>.Ok(
            result,
            "Đã tạo phiên ký điện tử và khóa version hợp đồng."));
    }

    [HttpPost("{sessionId:long}/director/sign")]
    [SessionAuthorize(RbacPermissions.ContractElectronicSigningDirector)]
    [ProducesResponseType(typeof(ApiResponse<DirectorSignElectronicContractResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SignAsDirector(
        long sessionId,
        [FromBody] DirectorSignElectronicContractRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        var result = await _service.SignAsDirectorAsync(sessionId, request, employeeId, cancellationToken);
        return Ok(ApiResponse<DirectorSignElectronicContractResponse>.Ok(result));
    }

    [HttpPost("{sessionId:long}/director/reject")]
    [SessionAuthorize(RbacPermissions.ContractElectronicSigningDirector)]
    public async Task<IActionResult> RejectAsDirector(
        long sessionId,
        [FromBody] RejectElectronicSigningRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = HttpContext.Session.GetInt32("EmployeeId")
            ?? throw new UnauthorizedAccessException("Employee login is required.");
        await _service.RejectAsDirectorAsync(sessionId, request, employeeId, cancellationToken);
        return NoContent();
    }
}
