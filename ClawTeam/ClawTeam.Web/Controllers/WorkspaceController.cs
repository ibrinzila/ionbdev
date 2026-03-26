using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Workspace (git worktree) management for agent isolation.</summary>
public class WorkspaceController : Controller
{
    private readonly string _dataDir;

    public WorkspaceController(IConfiguration config)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
    }

    private WorkspaceManager GetManager(string teamName) =>
        new(_dataDir, teamName);

    // GET /Workspace/Index/{teamName}
    public IActionResult Index(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var workspaces = GetManager(id).ListWorkspaces();
        ViewBag.TeamName = id;
        return View(workspaces);
    }

    // POST /Workspace/Checkpoint/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkpoint(string id, string agentName, string? message)
    {
        var success = GetManager(id).Checkpoint(agentName, message);
        TempData["Message"] = success
            ? $"Checkpoint created for {agentName}"
            : $"Checkpoint failed for {agentName}";
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Workspace/Merge/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Merge(string id, string agentName)
    {
        var success = GetManager(id).Merge(agentName);
        TempData["Message"] = success
            ? $"Merged {agentName}'s branch"
            : $"Merge failed for {agentName}";
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Workspace/Cleanup/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cleanup(string id, string agentName)
    {
        GetManager(id).CleanupWorkspace(agentName);
        TempData["Message"] = $"Workspace cleaned up for {agentName}";
        return RedirectToAction(nameof(Index), new { id });
    }
}
