using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ClaudeAgentTeamsUI.Hubs;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class TeamsController : Controller
{
    private readonly TeamService _teamService;
    private readonly TaskService _taskService;
    private readonly MessageService _messageService;
    private readonly IHubContext<TeamHub> _hubContext;

    public TeamsController(TeamService teamService, TaskService taskService,
        MessageService messageService, IHubContext<TeamHub> hubContext)
    {
        _teamService = teamService;
        _taskService = taskService;
        _messageService = messageService;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index()
    {
        var teams = await _teamService.GetAllAsync();
        return View(teams);
    }

    public IActionResult Create()
    {
        return View(new TeamCreateRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeamCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Team name is required");
            return View(request);
        }

        var team = await _teamService.CreateAsync(request);
        await _hubContext.Clients.Group("dashboard").SendAsync(TeamHub.Events.TeamUpdated, team.Id);
        return RedirectToAction(nameof(Detail), new { id = team.Id });
    }

    public async Task<IActionResult> Detail(string id)
    {
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var kanbanColumns = await _taskService.GetKanbanColumnsAsync(id);
        var messages = await _messageService.GetByTeamAsync(id);

        var vm = new TeamDetailViewModel
        {
            Team = team,
            KanbanColumns = kanbanColumns,
            RecentMessages = messages.Take(20).ToList(),
            UnreadMessageCount = await _messageService.GetUnreadCountAsync(id)
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Launch(string id)
    {
        var team = await _teamService.LaunchAsync(id, new TeamLaunchRequest());
        if (team == null) return NotFound();
        await _hubContext.Clients.Group("dashboard").SendAsync(TeamHub.Events.TeamUpdated, id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Stop(string id)
    {
        var team = await _teamService.StopAsync(id);
        if (team == null) return NotFound();
        await _hubContext.Clients.Group("dashboard").SendAsync(TeamHub.Events.TeamUpdated, id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        await _teamService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(string id, string name, MemberRole role)
    {
        await _teamService.UpdateAsync(id, team =>
        {
            team.Members.Add(new TeamMember { Name = name, Role = role });
        });
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember(string id, string memberId)
    {
        await _teamService.UpdateAsync(id, team =>
        {
            team.Members.RemoveAll(m => m.Id == memberId);
        });
        return RedirectToAction(nameof(Detail), new { id });
    }
}
