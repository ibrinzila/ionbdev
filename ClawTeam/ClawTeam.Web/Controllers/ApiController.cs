using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>
/// REST API endpoints matching the original ClawTeam CLI interface.
/// These allow agents to communicate with the coordination server via HTTP.
/// </summary>
[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly string _dataDir;
    private readonly TeamManager _teamManager;
    private readonly BoardCollector _collector;
    private readonly SpawnService _spawnService;

    public ApiController(IConfiguration config, TeamManager teamManager,
        BoardCollector collector, SpawnService spawnService)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
        _teamManager = teamManager;
        _collector = collector;
        _spawnService = spawnService;
    }

    // ── Overview ─────────────────────────────────────────────────────

    [HttpGet("overview")]
    public IActionResult GetOverview() => Ok(_collector.CollectOverview());

    // ── Teams ────────────────────────────────────────────────────────

    [HttpGet("team/{teamName}")]
    public IActionResult GetTeam(string teamName)
    {
        try { return Ok(_collector.CollectTeam(teamName)); }
        catch (InvalidOperationException) { return NotFound(new { error = $"Team '{teamName}' not found" }); }
    }

    [HttpPost("team")]
    public IActionResult CreateTeam([FromBody] CreateTeamForm form)
    {
        var config = _teamManager.CreateTeam(form.Name, form.Description, form.LeaderName, form.AgentType);
        return Ok(new { status = "ok", team = config.Name });
    }

    [HttpDelete("team/{teamName}")]
    public IActionResult DeleteTeam(string teamName)
    {
        _teamManager.Cleanup(teamName, force: true);
        return Ok(new { status = "ok" });
    }

    // ── Members ──────────────────────────────────────────────────────

    [HttpGet("team/{teamName}/members")]
    public IActionResult GetMembers(string teamName) =>
        Ok(_teamManager.ListMembers(teamName));

    [HttpPost("team/{teamName}/member")]
    public IActionResult AddMember(string teamName, [FromBody] SpawnAgentForm form)
    {
        var member = _teamManager.AddMember(teamName, form.AgentName, form.AgentType);
        return Ok(new { status = "ok", agentId = member.AgentId });
    }

    // ── Tasks ────────────────────────────────────────────────────────

    [HttpGet("team/{teamName}/tasks")]
    public IActionResult GetTasks(string teamName, string? status = null, string? owner = null)
    {
        var store = new TaskStore(_dataDir, teamName);
        return Ok(store.ListTasks(status, owner, sortByPriority: true));
    }

    [HttpPost("team/{teamName}/task")]
    public IActionResult CreateTask(string teamName, [FromBody] CreateTaskForm form)
    {
        var store = new TaskStore(_dataDir, teamName);
        var blockedBy = string.IsNullOrEmpty(form.BlockedBy)
            ? null
            : form.BlockedBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var task = store.Create(form.Subject, form.Description, form.Owner, form.Priority, blockedBy);
        return Ok(new { status = "ok", taskId = task.Id });
    }

    [HttpPut("team/{teamName}/task/{taskId}")]
    public IActionResult UpdateTask(string teamName, string taskId, [FromBody] UpdateTaskForm form)
    {
        var store = new TaskStore(_dataDir, teamName);
        try
        {
            var task = store.Update(taskId, form.Status, form.Owner, form.Priority);
            return Ok(new { status = "ok", task });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("team/{teamName}/task/{taskId}")]
    public IActionResult DeleteTask(string teamName, string taskId)
    {
        var store = new TaskStore(_dataDir, teamName);
        store.Delete(taskId);
        return Ok(new { status = "ok" });
    }

    // ── Inbox ────────────────────────────────────────────────────────

    [HttpPost("team/{teamName}/inbox/send")]
    public IActionResult SendMessage(string teamName, [FromBody] SendMessageForm form)
    {
        var mailbox = new MailboxManager(_dataDir, teamName);
        if (string.IsNullOrEmpty(form.To))
            mailbox.Broadcast(form.From, form.Content, form.Type);
        else
            mailbox.Send(form.From, form.To, form.Content, form.Type);
        return Ok(new { status = "ok" });
    }

    [HttpGet("team/{teamName}/inbox/{agentName}")]
    public IActionResult ReceiveMessages(string teamName, string agentName, bool peek = false)
    {
        var mailbox = new MailboxManager(_dataDir, teamName);
        var messages = peek ? mailbox.Peek(agentName) : mailbox.Receive(agentName);
        return Ok(messages);
    }

    [HttpGet("team/{teamName}/inbox/{agentName}/count")]
    public IActionResult InboxCount(string teamName, string agentName)
    {
        var mailbox = new MailboxManager(_dataDir, teamName);
        return Ok(new { count = mailbox.PeekCount(agentName) });
    }

    // ── Spawn ────────────────────────────────────────────────────────

    [HttpPost("team/{teamName}/spawn")]
    public IActionResult SpawnAgent(string teamName, [FromBody] SpawnAgentForm form)
    {
        try
        {
            var result = _spawnService.SpawnAgent(
                teamName, form.AgentName, form.AgentType, form.Task, form.Command);
            return Ok(new { status = "ok", message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    [HttpPost("team/{teamName}/lifecycle/idle")]
    public IActionResult MarkIdle(string teamName, [FromBody] Dictionary<string, string> body)
    {
        var agentName = body.GetValueOrDefault("agent_name", "");
        if (string.IsNullOrEmpty(agentName)) return BadRequest();

        var manager = new LifecycleManager(_dataDir, teamName, _teamManager);
        manager.SendIdle(agentName);
        return Ok(new { status = "ok" });
    }

    [HttpPost("team/{teamName}/lifecycle/shutdown")]
    public IActionResult RequestShutdown(string teamName, [FromBody] Dictionary<string, string> body)
    {
        var from = body.GetValueOrDefault("from", "");
        var target = body.GetValueOrDefault("target", "");
        var reason = body.GetValueOrDefault("reason");

        var manager = new LifecycleManager(_dataDir, teamName, _teamManager);
        var requestId = manager.RequestShutdown(from, target, reason);
        return Ok(new { status = "ok", requestId });
    }

    // ── Cost ──────────────────────────────────────────────────────────

    [HttpPost("team/{teamName}/cost")]
    public IActionResult ReportCost(string teamName, [FromBody] Models.CostEvent evt)
    {
        var store = new CostStore(_dataDir, teamName);
        store.Report(evt);
        return Ok(new { status = "ok" });
    }

    [HttpGet("team/{teamName}/cost")]
    public IActionResult GetCostSummary(string teamName)
    {
        var store = new CostStore(_dataDir, teamName);
        return Ok(store.Summary());
    }

    // ── Plans ─────────────────────────────────────────────────────────

    [HttpPost("team/{teamName}/plan")]
    public IActionResult SubmitPlan(string teamName, [FromBody] Models.SubmitPlanForm form)
    {
        var manager = new PlanManager(_dataDir, teamName, _teamManager);
        var planId = manager.SubmitPlan(form.AgentName, form.PlanContent, form.Summary);
        return Ok(new { status = "ok", planId });
    }

    [HttpPost("team/{teamName}/plan/{planId}/approve")]
    public IActionResult ApprovePlan(string teamName, string planId, [FromBody] Dictionary<string, string> body)
    {
        var from = body.GetValueOrDefault("from", "");
        var target = body.GetValueOrDefault("target", "");
        var feedback = body.GetValueOrDefault("feedback");
        var manager = new PlanManager(_dataDir, teamName, _teamManager);
        manager.ApprovePlan(from, planId, target, feedback);
        return Ok(new { status = "ok" });
    }

    [HttpPost("team/{teamName}/plan/{planId}/reject")]
    public IActionResult RejectPlan(string teamName, string planId, [FromBody] Dictionary<string, string> body)
    {
        var from = body.GetValueOrDefault("from", "");
        var target = body.GetValueOrDefault("target", "");
        var feedback = body.GetValueOrDefault("feedback");
        var manager = new PlanManager(_dataDir, teamName, _teamManager);
        manager.RejectPlan(from, planId, target, feedback);
        return Ok(new { status = "ok" });
    }

    [HttpGet("team/{teamName}/plan/{planId}")]
    public IActionResult GetPlan(string teamName, string planId, [FromQuery] string agent = "")
    {
        var manager = new PlanManager(_dataDir, teamName, _teamManager);
        var content = manager.GetPlan(agent, planId);
        if (content == null) return NotFound();
        return Ok(new { content });
    }

    // ── Workspace ────────────────────────────────────────────────────

    [HttpGet("team/{teamName}/workspaces")]
    public IActionResult ListWorkspaces(string teamName)
    {
        var manager = new WorkspaceManager(_dataDir, teamName);
        return Ok(manager.ListWorkspaces());
    }

    [HttpPost("team/{teamName}/workspace/{agentName}/checkpoint")]
    public IActionResult CheckpointWorkspace(string teamName, string agentName)
    {
        var manager = new WorkspaceManager(_dataDir, teamName);
        var success = manager.Checkpoint(agentName);
        return success ? Ok(new { status = "ok" }) : BadRequest(new { error = "Checkpoint failed" });
    }

    [HttpPost("team/{teamName}/workspace/{agentName}/merge")]
    public IActionResult MergeWorkspace(string teamName, string agentName)
    {
        var manager = new WorkspaceManager(_dataDir, teamName);
        var success = manager.Merge(agentName);
        return success ? Ok(new { status = "ok" }) : BadRequest(new { error = "Merge failed" });
    }

    [HttpDelete("team/{teamName}/workspace/{agentName}")]
    public IActionResult CleanupWorkspace(string teamName, string agentName)
    {
        var manager = new WorkspaceManager(_dataDir, teamName);
        manager.CleanupWorkspace(agentName);
        return Ok(new { status = "ok" });
    }

    // ── Member removal ───────────────────────────────────────────────

    [HttpDelete("team/{teamName}/member/{memberName}")]
    public IActionResult RemoveMember(string teamName, string memberName)
    {
        _teamManager.RemoveMember(teamName, memberName);
        return Ok(new { status = "ok" });
    }

    // ── Events (SSE) ─────────────────────────────────────────────────

    [HttpGet("events/{teamName}")]
    public async Task GetEvents(string teamName, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                object data;
                try { data = _collector.CollectTeam(teamName); }
                catch (Exception ex) { data = new { error = ex.Message }; }

                var json = System.Text.Json.JsonSerializer.Serialize(data,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
                await Task.Delay(2000, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
}
