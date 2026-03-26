using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ClaudeAgentTeamsUI.Hubs;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class TasksController : Controller
{
    private readonly TaskService _taskService;
    private readonly TeamService _teamService;
    private readonly IHubContext<TeamHub> _hubContext;

    public TasksController(TaskService taskService, TeamService teamService,
        IHubContext<TeamHub> hubContext)
    {
        _taskService = taskService;
        _teamService = teamService;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Kanban(string teamId)
    {
        var team = await _teamService.GetByIdAsync(teamId);
        if (team == null) return NotFound();

        var columns = await _taskService.GetKanbanColumnsAsync(teamId);
        var vm = new KanbanBoardViewModel
        {
            TeamId = teamId,
            TeamName = team.Name,
            Columns = columns
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string teamId, string title, string? description,
        string? assigneeId, string? assigneeName)
    {
        var task = await _taskService.CreateAsync(teamId, title, description, assigneeId, assigneeName);
        await _hubContext.Clients.Group($"team-{teamId}").SendAsync(TeamHub.Events.TaskUpdated, task);
        return RedirectToAction(nameof(Kanban), new { teamId });
    }

    public async Task<IActionResult> Detail(string id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null) return NotFound();
        return View(task);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(string id, TaskState status)
    {
        var task = await _taskService.UpdateStatusAsync(id, status);
        if (task == null) return NotFound();
        await _hubContext.Clients.Group($"team-{task.TeamId}").SendAsync(TeamHub.Events.TaskMoved, task);
        return RedirectToAction(nameof(Kanban), new { teamId = task.TeamId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null) return NotFound();
        await _taskService.DeleteAsync(id);
        return RedirectToAction(nameof(Kanban), new { teamId = task.TeamId });
    }
}

// API controller for AJAX/SignalR kanban drag-and-drop
[ApiController]
[Route("api/[controller]")]
public class TasksApiController : ControllerBase
{
    private readonly TaskService _taskService;
    private readonly IHubContext<TeamHub> _hubContext;

    public TasksApiController(TaskService taskService, IHubContext<TeamHub> hubContext)
    {
        _taskService = taskService;
        _hubContext = hubContext;
    }

    [HttpPost("move")]
    public async Task<IActionResult> Move([FromBody] TaskMoveRequest request)
    {
        var success = await _taskService.MoveAsync(request.TaskId, request.TargetColumn, request.Order);
        if (!success) return NotFound();

        var task = await _taskService.GetByIdAsync(request.TaskId);
        if (task != null)
            await _hubContext.Clients.Group($"team-{task.TeamId}").SendAsync(TeamHub.Events.TaskMoved, task);

        return Ok(new { success = true });
    }

    [HttpGet("{teamId}")]
    public async Task<IActionResult> GetByTeam(string teamId)
    {
        var columns = await _taskService.GetKanbanColumnsAsync(teamId);
        return Ok(columns);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
    {
        var task = await _taskService.CreateAsync(request.TeamId, request.Title,
            request.Description, request.AssigneeId, request.AssigneeName);
        await _hubContext.Clients.Group($"team-{request.TeamId}").SendAsync(TeamHub.Events.TaskUpdated, task);
        return Ok(task);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] StatusUpdateRequest request)
    {
        var task = await _taskService.UpdateStatusAsync(id, request.Status);
        if (task == null) return NotFound();
        await _hubContext.Clients.Group($"team-{task.TeamId}").SendAsync(TeamHub.Events.TaskUpdated, task);
        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _taskService.DeleteAsync(id);
        return success ? Ok() : NotFound();
    }
}

public class TaskMoveRequest
{
    public string TaskId { get; set; } = string.Empty;
    public TaskState TargetColumn { get; set; }
    public int Order { get; set; }
}

public class TaskCreateRequest
{
    public string TeamId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
}

public class StatusUpdateRequest
{
    public TaskState Status { get; set; }
}
