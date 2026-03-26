using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class SchedulesController : Controller
{
    private readonly ScheduleService _scheduleService;

    public SchedulesController(ScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public async Task<IActionResult> Index(string? teamFilter)
    {
        var schedules = await _scheduleService.GetAllAsync(teamFilter);
        var vm = new ScheduleListViewModel
        {
            Schedules = schedules,
            TeamFilter = teamFilter
        };
        return View(vm);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string teamName, string label, string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(cronExpression))
        {
            ModelState.AddModelError("", "Label and cron expression are required");
            return View();
        }

        await _scheduleService.CreateAsync(teamName, label, cronExpression);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Pause(string id)
    {
        await _scheduleService.PauseAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Resume(string id)
    {
        await _scheduleService.ResumeAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        await _scheduleService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
