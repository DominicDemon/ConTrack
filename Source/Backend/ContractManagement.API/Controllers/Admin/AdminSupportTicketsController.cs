using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Support;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Admin;

[ApiController]
[Route("api/admin/tickets")]
[AllowWithoutTenant]
[SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
public sealed class AdminSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _ticketService;

    public AdminSupportTicketsController(ISupportTicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? department,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var tickets = await _ticketService.GetAllForAdminAsync(
            status,
            priority,
            department,
            keyword,
            cancellationToken);

        return Ok(tickets);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var ticket = await _ticketService.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
        {
            return NotFound(new { message = "Không tìm thấy ticket hỗ trợ." });
        }

        return Ok(ticket);
    }

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> ReplyMessage(
        int id,
        [FromBody] AdminReplyTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageContent))
        {
            return BadRequest(new { message = "Vui lòng nhập nội dung phản hồi." });
        }

        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var adminName = HttpContext.Session.GetString(AccountSessionKeys.SystemAdminName) ?? "System Administrator";

        var message = await _ticketService.AddAdminMessageAsync(
            id,
            systemAdminId,
            adminName,
            request,
            cancellationToken);

        if (message is null)
        {
            return NotFound(new { message = "Không tìm thấy ticket hỗ trợ." });
        }

        return Ok(message);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateTicketStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var adminName = HttpContext.Session.GetString(AccountSessionKeys.SystemAdminName) ?? "System Administrator";

        var updated = await _ticketService.UpdateTicketStatusAsync(
            id,
            systemAdminId,
            adminName,
            request,
            cancellationToken);

        if (updated is null)
        {
            return NotFound(new { message = "Không tìm thấy ticket hỗ trợ." });
        }

        return Ok(updated);
    }
}

