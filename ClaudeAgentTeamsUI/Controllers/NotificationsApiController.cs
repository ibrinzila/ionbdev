using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsApiController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsApiController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var notifications = await _notificationService.GetAllAsync(limit, offset);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync();
        return Ok(new { count });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        await _notificationService.MarkReadAsync(id);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var count = await _notificationService.MarkAllReadAsync();
        return Ok(new { count });
    }
}
