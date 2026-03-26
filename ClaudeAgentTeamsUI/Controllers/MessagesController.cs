using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ClaudeAgentTeamsUI.Hubs;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class MessagesController : Controller
{
    private readonly MessageService _messageService;
    private readonly TeamService _teamService;
    private readonly IHubContext<TeamHub> _hubContext;

    public MessagesController(MessageService messageService, TeamService teamService,
        IHubContext<TeamHub> hubContext)
    {
        _messageService = messageService;
        _teamService = teamService;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index(string teamId)
    {
        var team = await _teamService.GetByIdAsync(teamId);
        if (team == null) return NotFound();

        var messages = await _messageService.GetByTeamAsync(teamId);
        var crossTeam = await _messageService.GetCrossTeamMessagesAsync(teamId);

        var vm = new MessagesViewModel
        {
            TeamId = teamId,
            TeamName = team.Name,
            Messages = messages,
            CrossTeamMessages = crossTeam,
            UnreadCount = await _messageService.GetUnreadCountAsync(teamId)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Send(string teamId, string from, string to, string content)
    {
        var msg = await _messageService.SendAsync(teamId, from, to, content);
        await _hubContext.Clients.Group($"team-{teamId}")
            .SendAsync(TeamHub.Events.MessageReceived, msg);
        return RedirectToAction(nameof(Index), new { teamId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(string id, string teamId)
    {
        await _messageService.MarkReadAsync(id);
        return RedirectToAction(nameof(Index), new { teamId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(string teamId)
    {
        await _messageService.MarkAllReadAsync(teamId);
        return RedirectToAction(nameof(Index), new { teamId });
    }
}
