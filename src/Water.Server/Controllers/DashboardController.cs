using Microsoft.AspNetCore.Mvc;
using Water.Observability;

namespace Water.Server.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly FlowDashboard? _dashboard;
    private readonly FlowRegistry _registry;

    public DashboardController(FlowRegistry registry, FlowDashboard? dashboard = null)
    {
        _registry = registry;
        _dashboard = dashboard;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (_dashboard == null)
            return NotFound(new { error = "Dashboard not configured (no storage backend)" });
        return Ok(await _dashboard.GetStatsAsync());
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] string? flowId = null, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        if (_dashboard == null)
            return NotFound(new { error = "Dashboard not configured" });
        return Ok(await _dashboard.GetSessionsListAsync(flowId, limit, offset));
    }

    [HttpGet("sessions/{executionId}")]
    public async Task<IActionResult> GetSessionDetail(string executionId)
    {
        if (_dashboard == null)
            return NotFound(new { error = "Dashboard not configured" });
        var detail = await _dashboard.GetSessionDetailAsync(executionId);
        if (detail == null)
            return NotFound(new { error = "Session not found" });
        return Ok(detail);
    }

    [HttpGet("flows")]
    public IActionResult GetFlows()
    {
        var flows = _registry.GetAll().Select(f => new
        {
            id = f.Id,
            description = f.Description,
            task_count = f.ExecutionGraph.Count
        });
        return Ok(new { flows });
    }
}
