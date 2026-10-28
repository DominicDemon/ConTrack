using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Support;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Support;

[ApiController]
[Route("api/support/tickets")]
[SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
public sealed class CustomerSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _ticketService;
    private readonly ICurrentTenant _currentTenant;

    public CustomerSupportTicketsController(
        ISupportTicketService ticketService,
        ICurrentTenant currentTenant)
    {
        _ticketService = ticketService;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var tenant = _currentTenant.GetRequiredTenant();
        var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext);
        var employeeId = employee?.EmployeeId ?? 0;

        var tickets = await _ticketService.GetAllForTenantAsync(
            tenant.TenantId,
            tenant.TenantCode,
            employeeId,
            status,
            priority,
            keyword,
            cancellationToken);

        return Ok(tickets);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var tenant = _currentTenant.GetRequiredTenant();
        var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext);
        var employeeId = employee?.EmployeeId ?? 0;

        var ticket = await _ticketService.GetByIdForCustomerAsync(
            id,
            tenant.TenantId,
            tenant.TenantCode,
            employeeId,
            cancellationToken);
        if (ticket is null)
        {
            return NotFound(new { message = "Không tìm thấy yêu cầu hỗ trợ." });
        }

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupportTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResolvedSubject)
            || string.IsNullOrWhiteSpace(request.ResolvedDescription))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ tiêu đề và nội dung yêu cầu hỗ trợ." });
        }

        var tenant = _currentTenant.GetRequiredTenant();
        var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext);
        var employeeId = employee?.EmployeeId ?? 0;
        var senderName = !string.IsNullOrWhiteSpace(employee?.FullName)
            ? employee.FullName
            : employee?.Account ?? tenant.TenantName;

        var created = await _ticketService.CreateCustomerTicketAsync(
            tenant.TenantId,
            tenant.TenantCode,
            tenant.TenantName,
            employeeId,
            senderName,
            request,
            cancellationToken);

        return Ok(created);
    }

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> AddMessage(
        int id,
        [FromBody] CustomerReplyTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var content = request.MessageContent ?? request.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { message = "Vui lòng nhập nội dung phản hồi." });
        }

        var tenant = _currentTenant.GetRequiredTenant();
        var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext);
        var employeeId = employee?.EmployeeId ?? 0;
        var senderName = !string.IsNullOrWhiteSpace(employee?.FullName)
            ? employee.FullName
            : employee?.Account ?? tenant.TenantName;

        var msg = await _ticketService.AddCustomerMessageAsync(
            id,
            tenant.TenantId,
            tenant.TenantCode,
            tenant.TenantName,
            employeeId,
            senderName,
            request,
            cancellationToken);

        if (msg is null)
        {
            return NotFound(new { message = "Không tìm thấy yêu cầu hỗ trợ." });
        }

        return Ok(msg);
    }
}

