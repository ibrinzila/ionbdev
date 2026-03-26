namespace ClawTeam.Web.Models;

/// <summary>View model for the team overview / home page.</summary>
public class TeamOverviewViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Leader { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int TaskCount { get; set; }
    public int PendingMessages { get; set; }
}

/// <summary>View model for the team detail / board page.</summary>
public class TeamDetailViewModel
{
    public TeamConfig Team { get; set; } = new();
    public string LeaderName { get; set; } = string.Empty;
    public List<MemberViewModel> Members { get; set; } = new();
    public Dictionary<string, List<TaskItem>> Tasks { get; set; } = new();
    public TaskSummaryViewModel TaskSummary { get; set; } = new();
    public List<TeamMessage> Messages { get; set; } = new();
    public CostSummary? Cost { get; set; }
}

public class MemberViewModel
{
    public string Name { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public string JoinedAt { get; set; } = string.Empty;
    public string? User { get; set; }
    public string InboxName { get; set; } = string.Empty;
    public int InboxCount { get; set; }
}

public class TaskSummaryViewModel
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Blocked { get; set; }
}

/// <summary>Form model for creating a new team.</summary>
public class CreateTeamForm
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LeaderName { get; set; } = string.Empty;
    public string AgentType { get; set; } = "claude";
}

/// <summary>Form model for creating a new task.</summary>
public class CreateTaskForm
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public string Priority { get; set; } = "medium";
    public string? BlockedBy { get; set; }
}

/// <summary>Form model for updating a task.</summary>
public class UpdateTaskForm
{
    public string? Status { get; set; }
    public string? Owner { get; set; }
    public string? Priority { get; set; }
}

/// <summary>Form model for sending a message.</summary>
public class SendMessageForm
{
    public string From { get; set; } = string.Empty;
    public string? To { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "chat";
}

/// <summary>Form model for spawning an agent.</summary>
public class SpawnAgentForm
{
    public string AgentName { get; set; } = string.Empty;
    public string AgentType { get; set; } = "claude";
    public string? Task { get; set; }
    public string? Command { get; set; }
}

/// <summary>Form model for submitting a plan.</summary>
public class SubmitPlanForm
{
    public string AgentName { get; set; } = string.Empty;
    public string PlanContent { get; set; } = string.Empty;
    public string? Summary { get; set; }
}
