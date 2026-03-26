using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Plan approval workflow: submit, approve, reject plans.</summary>
public class PlanController : Controller
{
    private readonly string _dataDir;
    private readonly TeamManager _teamManager;

    public PlanController(IConfiguration config, TeamManager teamManager)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
        _teamManager = teamManager;
    }

    private PlanManager GetManager(string teamName) =>
        new(_dataDir, teamName, _teamManager);

    // GET /Plan/Index/{teamName}
    public IActionResult Index(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var plans = GetManager(id).ListPlans();
        ViewBag.TeamName = id;
        return View(plans);
    }

    // GET /Plan/View/{teamName}?agent={agent}&planId={planId}
    public IActionResult View(string id, string agent, string planId)
    {
        var content = GetManager(id).GetPlan(agent, planId);
        if (content == null) return NotFound();

        ViewBag.TeamName = id;
        ViewBag.AgentName = agent;
        ViewBag.PlanId = planId;
        ViewBag.PlanContent = content;
        return View("ViewPlan");
    }

    // POST /Plan/Submit/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Submit(string id, SubmitPlanForm form)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(form.AgentName))
            return BadRequest();

        var planId = GetManager(id).SubmitPlan(form.AgentName, form.PlanContent, form.Summary);
        TempData["Message"] = $"Plan submitted: {planId}";
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Plan/Approve/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Approve(string id, string fromAgent, string planId, string targetAgent, string? feedback)
    {
        GetManager(id).ApprovePlan(fromAgent, planId, targetAgent, feedback);
        TempData["Message"] = $"Plan {planId} approved";
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Plan/Reject/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reject(string id, string fromAgent, string planId, string targetAgent, string? feedback)
    {
        GetManager(id).RejectPlan(fromAgent, planId, targetAgent, feedback);
        TempData["Message"] = $"Plan {planId} rejected";
        return RedirectToAction(nameof(Index), new { id });
    }
}
