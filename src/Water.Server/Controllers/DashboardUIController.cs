using Microsoft.AspNetCore.Mvc;
using Water.Observability;
using Water.Server.Models;
using System.Text.Json;

namespace Water.Server.Controllers;

public class DashboardUIController : Controller
{
    private readonly FlowDashboard _dashboard;

    public DashboardUIController(FlowDashboard dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _dashboard.GetStatsAsync();
        var sessions = await _dashboard.GetSessionsListAsync(limit: 50);

        var total = stats.TryGetValue("total_sessions", out var ts) && ts is int t ? t : 0;
        var completed = stats.TryGetValue("completed", out var c) && c is int cv ? cv : 0;

        var model = new DashboardViewModel
        {
            TotalSessions = total,
            Running = stats.TryGetValue("running", out var r) && r is int rv ? rv : 0,
            Completed = completed,
            Failed = stats.TryGetValue("failed", out var f) && f is int fv ? fv : 0,
            Paused = stats.TryGetValue("paused", out var p) && p is int pv ? pv : 0,
            SuccessRate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
            Sessions = sessions.Select(s => new SessionRowViewModel
            {
                ExecutionId = s.TryGetValue("execution_id", out var eid) ? eid?.ToString() ?? "" : "",
                FlowId = s.TryGetValue("flow_id", out var fid) ? fid?.ToString() ?? "" : "",
                Status = s.TryGetValue("status", out var st) ? st?.ToString() ?? "" : "",
                CreatedAt = s.TryGetValue("created_at", out var ca) ? ca?.ToString() ?? "" : "",
                UpdatedAt = s.TryGetValue("updated_at", out var ua) ? ua?.ToString() ?? "" : "",
                Error = s.TryGetValue("error", out var err) ? err?.ToString() : null
            }).ToList()
        };

        ViewData["Title"] = "Dashboard";
        return View(model);
    }

    public async Task<IActionResult> Session(string id)
    {
        var detail = await _dashboard.GetSessionDetailAsync(id);
        if (detail == null) return NotFound();

        var taskRuns = detail.TryGetValue("task_runs", out var tr) && tr is List<Dictionary<string, object?>> runs
            ? runs : new List<Dictionary<string, object?>>();

        var model = new SessionDetailViewModel
        {
            ExecutionId = detail.TryGetValue("execution_id", out var eid) ? eid?.ToString() ?? "" : "",
            FlowId = detail.TryGetValue("flow_id", out var fid) ? fid?.ToString() ?? "" : "",
            Status = detail.TryGetValue("status", out var st) ? st?.ToString() ?? "" : "",
            CreatedAt = detail.TryGetValue("created_at", out var ca) ? ca?.ToString() ?? "" : "",
            UpdatedAt = detail.TryGetValue("updated_at", out var ua) ? ua?.ToString() ?? "" : "",
            Error = detail.TryGetValue("error", out var err) ? err?.ToString() : null,
            InputDataJson = detail.TryGetValue("input_data", out var inp) ? JsonSerializer.Serialize(inp, new JsonSerializerOptions { WriteIndented = true }) : "{}",
            CurrentDataJson = detail.TryGetValue("current_data", out var cur) ? JsonSerializer.Serialize(cur, new JsonSerializerOptions { WriteIndented = true }) : "{}",
            ResultJson = detail.TryGetValue("result", out var res) ? JsonSerializer.Serialize(res, new JsonSerializerOptions { WriteIndented = true }) : "{}",
            TaskRuns = taskRuns.Select(run => new TaskRunViewModel
            {
                Id = run.TryGetValue("id", out var rid) ? rid?.ToString() ?? "" : "",
                TaskId = run.TryGetValue("task_id", out var tid) ? tid?.ToString() ?? "" : "",
                Status = run.TryGetValue("status", out var s) ? s?.ToString() ?? "" : "",
                StartedAt = run.TryGetValue("started_at", out var sa) ? sa?.ToString() : null,
                CompletedAt = run.TryGetValue("completed_at", out var cpa) ? cpa?.ToString() : null,
                Error = run.TryGetValue("error", out var e) ? e?.ToString() : null
            }).ToList()
        };

        ViewData["Title"] = $"Session: {model.ExecutionId[..Math.Min(8, model.ExecutionId.Length)]}...";
        return View(model);
    }
}
