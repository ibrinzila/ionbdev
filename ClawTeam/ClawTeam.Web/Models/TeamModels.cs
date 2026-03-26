using System.Text.Json.Serialization;

namespace ClawTeam.Web.Models;

/// <summary>Status of a team member.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberStatus
{
    Active,
    Idle,
    Shutdown
}

/// <summary>Status of a task on the kanban board.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Blocked
}

/// <summary>Priority level for tasks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent
}

/// <summary>Types of inter-agent messages.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    Chat,
    StatusUpdate,
    TaskAssignment,
    JoinRequest,
    JoinApproved,
    PlanApprovalRequest,
    PlanApproved,
    PlanRejected,
    ShutdownRequest,
    ShutdownApproved,
    ShutdownRejected,
    Idle,
    Broadcast
}

/// <summary>A member of a team.</summary>
public class TeamMember
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("agent_type")]
    public string AgentType { get; set; } = "claude";

    [JsonPropertyName("joined_at")]
    public string JoinedAt { get; set; } = DateTime.UtcNow.ToString("o");
}

/// <summary>Configuration / metadata for a team.</summary>
public class TeamConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("lead_agent_id")]
    public string LeadAgentId { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("members")]
    public List<TeamMember> Members { get; set; } = new();

    [JsonPropertyName("budget_cents")]
    public int BudgetCents { get; set; }
}

/// <summary>A message exchanged between agents.</summary>
public class TeamMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("type")]
    public string Type { get; set; } = "chat";

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("team")]
    public string Team { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; set; }

    [JsonPropertyName("plan_id")]
    public string? PlanId { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>A task on the shared kanban board.</summary>
public class TaskItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "medium";

    [JsonPropertyName("owner")]
    public string? Owner { get; set; }

    [JsonPropertyName("locked_by")]
    public string? LockedBy { get; set; }

    [JsonPropertyName("blocks")]
    public List<string> Blocks { get; set; } = new();

    [JsonPropertyName("blocked_by")]
    public List<string> BlockedBy { get; set; } = new();

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; set; }

    [JsonPropertyName("duration_seconds")]
    public double? DurationSeconds { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
