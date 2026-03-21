using Microsoft.AspNetCore.Mvc;
using Water.Core;
using Water.Observability;
using Water.Server.Models;
using System.Text.Json;

namespace Water.Server.Controllers;

public class FlowsUIController : Controller
{
    private readonly FlowRegistry _registry;
    private readonly FlowDashboard _dashboard;

    public FlowsUIController(FlowRegistry registry, FlowDashboard dashboard)
    {
        _registry = registry;
        _dashboard = dashboard;
    }

    public IActionResult Index()
    {
        var flows = _registry.GetAll().Select(f => new FlowCardViewModel
        {
            Id = f.Id,
            Description = f.Description,
            TaskCount = f.ExecutionGraph.Count,
            Tasks = ExtractTaskInfo(f)
        }).ToList();

        ViewData["Title"] = "Flows";
        return View(flows);
    }

    public async Task<IActionResult> Detail(string id)
    {
        var flow = _registry.Get(id);
        if (flow == null) return NotFound();

        var sessions = await _dashboard.GetSessionsListAsync(id, limit: 20);

        var model = new FlowDetailViewModel
        {
            Id = flow.Id,
            Description = flow.Description,
            Metadata = flow.Metadata,
            Tasks = ExtractTaskInfo(flow),
            Sessions = sessions.Select(s => new SessionRowViewModel
            {
                ExecutionId = s.TryGetValue("execution_id", out var eid) ? eid?.ToString() ?? "" : "",
                FlowId = s.TryGetValue("flow_id", out var fid) ? fid?.ToString() ?? "" : "",
                Status = s.TryGetValue("status", out var st) ? st?.ToString() ?? "" : "",
                CreatedAt = s.TryGetValue("created_at", out var ca) ? ca?.ToString() ?? "" : "",
                Error = s.TryGetValue("error", out var err) ? err?.ToString() : null
            }).ToList()
        };

        ViewData["Title"] = $"Flow: {flow.Id}";
        return View(model);
    }

    public IActionResult Run(string id)
    {
        var flow = _registry.Get(id);
        if (flow == null) return NotFound();

        ViewData["Title"] = $"Run: {flow.Id}";
        return View(new RunFlowViewModel { FlowId = flow.Id, Description = flow.Description });
    }

    [HttpPost]
    public async Task<IActionResult> Execute(string id, [FromForm] string inputJson)
    {
        var flow = _registry.Get(id);
        if (flow == null) return NotFound();

        try
        {
            var input = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson ?? "{}") ?? new();
            var start = DateTime.UtcNow;
            var result = await flow.RunAsync(input);
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

            TempData["Success"] = $"Flow executed in {elapsed:F1}ms";
            TempData["Result"] = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Run", new { id });
    }

    private static List<TaskInfoDto> ExtractTaskInfo(Flow flow)
    {
        var infos = new List<TaskInfoDto>();
        foreach (var node in flow.ExecutionGraph)
        {
            switch (node.Type)
            {
                case NodeType.Sequential when node.Task != null:
                    infos.Add(new TaskInfoDto { Id = node.Task.Id, Description = node.Task.Description, Type = "sequential" });
                    break;
                case NodeType.Parallel when node.Tasks != null:
                    foreach (var t in node.Tasks)
                        infos.Add(new TaskInfoDto { Id = t.Id, Description = t.Description, Type = "parallel" });
                    break;
                case NodeType.Branch when node.Branches != null:
                    foreach (var b in node.Branches)
                        infos.Add(new TaskInfoDto { Id = b.Task.Id, Description = b.Task.Description, Type = "branch" });
                    break;
                case NodeType.Loop when node.Task != null:
                    infos.Add(new TaskInfoDto { Id = node.Task.Id, Description = node.Task.Description, Type = "loop" });
                    break;
                case NodeType.Dag when node.Tasks != null:
                    foreach (var t in node.Tasks)
                        infos.Add(new TaskInfoDto { Id = t.Id, Description = t.Description, Type = "dag" });
                    break;
                case NodeType.Map when node.Task != null:
                    infos.Add(new TaskInfoDto { Id = node.Task.Id, Description = node.Task.Description, Type = "map" });
                    break;
            }
        }
        return infos;
    }
}
