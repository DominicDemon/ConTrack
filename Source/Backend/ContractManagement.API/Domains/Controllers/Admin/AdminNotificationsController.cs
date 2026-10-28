using ContractManagement.API.Domains.Support;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
[AllowWithoutTenant]
[SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly ISupportTicketService _ticketService;

    public AdminNotificationsController(ISupportTicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status = "all",
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, unreadCount) = await _ticketService.GetAdminNotificationsAsync(
            status,
            limit,
            cancellationToken);

        return Ok(new
        {
            items,
            unreadCount
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken = default)
    {
        var count = await _ticketService.GetAdminUnreadNotificationCountAsync(cancellationToken);
        return Ok(new { count });
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(
        int id,
        CancellationToken cancellationToken = default)
    {
        var found = await _ticketService.MarkAdminNotificationReadAsync(id, cancellationToken);
        if (!found)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var changed = await _ticketService.MarkAllAdminNotificationsReadAsync(cancellationToken);
        return Ok(new { changed });
    }
}

