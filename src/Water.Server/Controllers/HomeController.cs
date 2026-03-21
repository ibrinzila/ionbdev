using Microsoft.AspNetCore.Mvc;
using Water.Observability;
using Water.Server.Models;

namespace Water.Server.Controllers;

public class HomeController : Controller
{
    private readonly FlowRegistry _registry;
    private readonly FlowDashboard _dashboard;

    public HomeController(FlowRegistry registry, FlowDashboard dashboard)
    {
        _registry = registry;
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _dashboard.GetStatsAsync();
        var sessions = await _dashboard.GetSessionsListAsync(limit: 5);
        var flows = _registry.GetAll();

        var model = new HomeViewModel
        {
            TotalFlows = flows.Count,
            TotalSessions = stats.TryGetValue("total_sessions", out var ts) && ts is int t ? t : 0,
            Running = stats.TryGetValue("running", out var r) && r is int rv ? rv : 0,
            Completed = stats.TryGetValue("completed", out var c) && c is int cv ? cv : 0,
            Failed = stats.TryGetValue("failed", out var f) && f is int fv ? fv : 0,
            Paused = stats.TryGetValue("paused", out var p) && p is int pv ? pv : 0,
            RecentFlows = flows.Select(fl => new FlowCardViewModel
            {
                Id = fl.Id,
                Description = fl.Description,
                TaskCount = fl.ExecutionGraph.Count
            }).ToList(),
            RecentSessions = sessions.Select(s => new SessionRowViewModel
            {
                ExecutionId = s.TryGetValue("execution_id", out var eid) ? eid?.ToString() ?? "" : "",
                FlowId = s.TryGetValue("flow_id", out var fid) ? fid?.ToString() ?? "" : "",
                Status = s.TryGetValue("status", out var st) ? st?.ToString() ?? "" : "",
                CreatedAt = s.TryGetValue("created_at", out var ca) ? ca?.ToString() ?? "" : "",
                Error = s.TryGetValue("error", out var err) ? err?.ToString() : null
            }).ToList()
        };

        ViewData["Title"] = "Home";
        return View(model);
    }
}
