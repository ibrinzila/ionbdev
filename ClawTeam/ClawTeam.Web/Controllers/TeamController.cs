using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Team management: create, view, delete teams and members.</summary>
public class TeamController : Controller
{
    private readonly TeamManager _teamManager;
    private readonly BoardCollector _collector;

    public TeamController(TeamManager teamManager, BoardCollector collector)
    {
        _teamManager = teamManager;
        _collector = collector;
    }

    // GET /Team
    public IActionResult Index()
    {
        var teams = _collector.CollectOverview();
        return View(teams);
    }

    // GET /Team/Details/{name}
    public IActionResult Details(string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("Team name is required");

        try
        {
            var detail = _collector.CollectTeam(id);
            return View(detail);
        }
        catch (InvalidOperationException)
        {
            return NotFound($"Team '{id}' not found");
        }
    }

    // GET /Team/Create
    public IActionResult Create()
    {
        return View(new CreateTeamForm());
    }

    // POST /Team/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateTeamForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.LeaderName))
        {
            ModelState.AddModelError("", "Team name and leader name are required");
            return View(form);
        }

        _teamManager.CreateTeam(form.Name, form.Description, form.LeaderName, form.AgentType);
        return RedirectToAction(nameof(Details), new { id = form.Name });
    }

    // POST /Team/AddMember/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddMember(string id, string memberName, string agentType = "claude")
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(memberName))
            return BadRequest();

        _teamManager.AddMember(id, memberName, agentType);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Team/RemoveMember/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveMember(string id, string memberName)
    {
        _teamManager.RemoveMember(id, memberName);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Team/Delete/{name}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        _teamManager.Cleanup(id, force: true);
        return RedirectToAction(nameof(Index));
    }
}
