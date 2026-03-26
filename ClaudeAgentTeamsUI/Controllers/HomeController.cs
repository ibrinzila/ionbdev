using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class HomeController : Controller
{
    private readonly TeamService _teamService;
    private readonly TaskService _taskService;
    private readonly SessionService _sessionService;
    private readonly ProjectService _projectService;
    private readonly NotificationService _notificationService;

    public HomeController(TeamService teamService, TaskService taskService,
        SessionService sessionService, ProjectService projectService,
        NotificationService notificationService)
    {
        _teamService = teamService;
        _taskService = taskService;
        _sessionService = sessionService;
        _projectService = projectService;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var teams = await _teamService.GetAllAsync();
        var projects = await _projectService.GetAllAsync();
        var notifications = await _notificationService.GetAllAsync(5);

        int pendingTasks = 0, completedTasks = 0, activeAgents = 0;
        foreach (var team in teams)
        {
            var stats = await _taskService.GetStatsAsync(team.Id);
            pendingTasks += stats.Pending;
            completedTasks += stats.Completed;
            activeAgents += team.Members.Count(m => m.Status == MemberStatus.Active);
        }

        var process = Process.GetCurrentProcess();
        var vm = new DashboardViewModel
        {
            Teams = teams.Select(t => new TeamSummary
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                MemberCount = t.Members.Count,
                ActiveMembers = t.Members.Count(m => m.Status == MemberStatus.Active),
                LastActivity = t.UpdatedAt
            }).ToList(),
            RecentProjects = projects.Take(5).ToList(),
            TotalSessions = await _sessionService.GetTotalCountAsync(),
            ActiveAgents = activeAgents,
            PendingTasks = pendingTasks,
            CompletedTasks = completedTasks,
            UnreadNotifications = await _notificationService.GetUnreadCountAsync(),
            RecentNotifications = notifications,
            Health = new SystemHealth
            {
                MemoryUsageMb = process.WorkingSet64 / 1024 / 1024,
                CpuPercent = 0,
                Uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(),
                DotNetVersion = Environment.Version.ToString()
            }
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
