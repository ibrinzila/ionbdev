using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Task management: CRUD operations on the shared kanban board.</summary>
public class TaskController : Controller
{
    private readonly string _dataDir;

    public TaskController(IConfiguration config)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
    }

    private TaskStore Store(string teamName) => new(_dataDir, teamName);

    // GET /Task/Index/{teamName}
    public IActionResult Index(string id, string? status = null, string? owner = null)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("Team name is required");

        var tasks = Store(id).ListTasks(status, owner, sortByPriority: true);
        ViewBag.TeamName = id;
        return View(tasks);
    }

    // GET /Task/Create/{teamName}
    public IActionResult Create(string id)
    {
        ViewBag.TeamName = id;
        return View(new CreateTaskForm());
    }

    // POST /Task/Create/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(string id, CreateTaskForm form)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(form.Subject))
        {
            ModelState.AddModelError("", "Team name and subject are required");
            ViewBag.TeamName = id;
            return View(form);
        }

        var blockedBy = string.IsNullOrEmpty(form.BlockedBy)
            ? null
            : form.BlockedBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        Store(id).Create(form.Subject, form.Description, form.Owner, form.Priority, blockedBy);
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Task/Update/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(string id, string taskId, UpdateTaskForm form)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(taskId))
            return BadRequest();

        Store(id).Update(taskId, form.Status, form.Owner, form.Priority);
        return RedirectToAction(nameof(Index), new { id });
    }

    // POST /Task/Delete/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id, string taskId)
    {
        Store(id).Delete(taskId);
        return RedirectToAction(nameof(Index), new { id });
    }
}
