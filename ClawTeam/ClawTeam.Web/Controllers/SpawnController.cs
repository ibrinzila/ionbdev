using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Agent spawning operations.</summary>
public class SpawnController : Controller
{
    private readonly SpawnService _spawnService;

    public SpawnController(SpawnService spawnService)
    {
        _spawnService = spawnService;
    }

    // GET /Spawn/Index/{teamName}
    public IActionResult Index(string id)
    {
        ViewBag.TeamName = id;
        var running = _spawnService.ListRunning();
        return View(running);
    }

    // GET /Spawn/Create/{teamName}
    public IActionResult Create(string id)
    {
        ViewBag.TeamName = id;
        return View(new SpawnAgentForm());
    }

    // POST /Spawn/Create/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(string id, SpawnAgentForm form)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(form.AgentName))
        {
            ModelState.AddModelError("", "Team name and agent name are required");
            ViewBag.TeamName = id;
            return View(form);
        }

        try
        {
            var result = _spawnService.SpawnAgent(
                id, form.AgentName, form.AgentType, form.Task, form.Command);
            TempData["Message"] = result;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { id });
    }
}
