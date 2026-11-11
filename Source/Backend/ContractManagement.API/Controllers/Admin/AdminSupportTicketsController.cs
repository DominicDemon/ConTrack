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
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 0;
        var tickets = await _ticketService.GetAllForAdminAsync(
            systemAdminId,
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
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 0;
        var ticket = await _ticketService.GetByIdAsync(id, systemAdminId, cancellationToken);
        if (ticket is null)
        {
            return NotFound(new { message = "Không tìm thấy ticket hỗ trợ hoặc bạn không có quyền truy cập." });
        }

        return Ok(ticket);
    }

    [HttpGet("{id:int}/staff")]
    public async Task<IActionResult> GetDepartmentStaff(
        int id,
        CancellationToken cancellationToken)
    {
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var staffList = await _ticketService.GetDepartmentStaffAsync(id, systemAdminId, cancellationToken);
        return Ok(staffList);
    }

    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> AssignToStaff(
        int id,
        [FromBody] AssignTicketToStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var adminName = HttpContext.Session.GetString(AccountSessionKeys.SystemAdminName) ?? "System Administrator";

        var updated = await _ticketService.AssignToStaffAsync(
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

    [HttpPost("{id:int}/staff-complete")]
    public async Task<IActionResult> StaffComplete(
        int id,
        [FromBody] StaffCompleteTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Resolution))
        {
            return BadRequest(new { message = "Vui lòng nhập phương án/kết quả xử lý." });
        }

        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var adminName = HttpContext.Session.GetString(AccountSessionKeys.SystemAdminName) ?? "Nhân viên xử lý";

        var updated = await _ticketService.StaffCompleteAsync(
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

    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> HeadResolve(
        int id,
        [FromBody] HeadResolveTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var systemAdminId = HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId) ?? 1;
        var adminName = HttpContext.Session.GetString(AccountSessionKeys.SystemAdminName) ?? "System Administrator";

        var updated = await _ticketService.HeadResolveAsync(
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

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> ReplyMessage(
        int id,
        [FromBody] AdminReplyTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var hasImage = !string.IsNullOrWhiteSpace(request.AttachmentUrl)
            || (request.AttachmentUrls is { Count: > 0 } && request.AttachmentUrls.Any(u => !string.IsNullOrWhiteSpace(u)));
        if (string.IsNullOrWhiteSpace(request.MessageContent) && !hasImage)
        {
            return BadRequest(new { message = "Vui lòng nhập nội dung tin nhắn hoặc chọn hình ảnh để gửi." });
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
