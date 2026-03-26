using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Agent lifecycle: shutdown requests, approvals, idle notifications.</summary>
public class LifecycleController : Controller
{
    private readonly string _dataDir;
    private readonly TeamManager _teamManager;

    public LifecycleController(IConfiguration config, TeamManager teamManager)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
        _teamManager = teamManager;
    }

    private LifecycleManager GetManager(string teamName) =>
        new(_dataDir, teamName, _teamManager);

    // POST /Lifecycle/RequestShutdown/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RequestShutdown(string id, string fromAgent, string targetAgent, string? reason)
    {
        var requestId = GetManager(id).RequestShutdown(fromAgent, targetAgent, reason);
        TempData["Message"] = $"Shutdown requested: {requestId}";
        return RedirectToAction("Details", "Team", new { id });
    }

    // POST /Lifecycle/ApproveShutdown/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveShutdown(string id, string fromAgent, string requestId, string targetAgent)
    {
        GetManager(id).ApproveShutdown(fromAgent, requestId, targetAgent);
        TempData["Message"] = "Shutdown approved";
        return RedirectToAction("Details", "Team", new { id });
    }

    // POST /Lifecycle/Idle/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Idle(string id, string agentName)
    {
        GetManager(id).SendIdle(agentName);
        TempData["Message"] = $"{agentName} marked idle";
        return RedirectToAction("Details", "Team", new { id });
    }

    // POST /Lifecycle/Cleanup/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cleanup(string id)
    {
        GetManager(id).CleanupTeam(force: true);
        TempData["Message"] = $"Team '{id}' cleaned up";
        return RedirectToAction("Index", "Home");
    }
}
