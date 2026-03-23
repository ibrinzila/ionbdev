using System.Text.Json.Serialization;

namespace Codapter.Core.Models;

/// <summary>
/// Collaboration agent types.
/// Port of packages/core/src/collab-types.ts
/// </summary>

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollabAgentStatus
{
    PendingInit,
    Running,
    Interrupted,
    Completed,
    Errored,
    Shutdown,
    NotFound
}

public record CollabAgent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("nickname")]
    public string Nickname { get; init; } = default!;

    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = default!;

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("status")]
    public CollabAgentStatus Status { get; init; }

    [JsonPropertyName("completionMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletionMessage { get; init; }
}

public record CollabAgentState
{
    [JsonPropertyName("status")]
    public CollabAgentStatus Status { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollabToolCallType
{
    SpawnAgent,
    SendInput,
    Wait,
    CloseAgent,
    ResumeAgent
}

public record CollabAgentToolCall
{
    [JsonPropertyName("type")]
    public CollabToolCallType Type { get; init; }

    [JsonPropertyName("agentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentId { get; init; }

    [JsonPropertyName("agentIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AgentIds { get; init; }
}

public record CollabConfig
{
    [JsonPropertyName("maxAgents")]
    public int MaxAgents { get; init; } = 5;

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; init; } = 3;

    [JsonPropertyName("minTimeoutMs")]
    public int MinTimeoutMs { get; init; } = 10_000;

    [JsonPropertyName("maxTimeoutMs")]
    public int MaxTimeoutMs { get; init; } = 3_600_000;

    [JsonPropertyName("defaultTimeoutMs")]
    public int DefaultTimeoutMs { get; init; } = 300_000;
}

#region Spawn

public record SpawnAgentRequest
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = default!;

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }
}

public record SpawnAgentResponse
{
    [JsonPropertyName("agent")]
    public CollabAgent Agent { get; init; } = default!;
}

#endregion

#region SendInput

public record SendInputRequest
{
    [JsonPropertyName("agentId")]
    public string AgentId { get; init; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("interrupt")]
    public bool Interrupt { get; init; }
}

public record SendInputResponse
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }
}

#endregion

#region Wait

public record WaitAgentRequest
{
    [JsonPropertyName("agentIds")]
    public List<string> AgentIds { get; init; } = new();

    [JsonPropertyName("timeoutMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMs { get; init; }
}

public record WaitAgentResponse
{
    [JsonPropertyName("results")]
    public Dictionary<string, CollabAgentState> Results { get; init; } = new();
}

#endregion

#region Close / Resume

public record CloseAgentRequest
{
    [JsonPropertyName("agentId")]
    public string AgentId { get; init; } = default!;
}

public record CloseAgentResponse
{
    [JsonPropertyName("status")]
    public CollabAgentStatus Status { get; init; }
}

public record ResumeAgentRequest
{
    [JsonPropertyName("agentId")]
    public string AgentId { get; init; } = default!;
}

public record ResumeAgentResponse
{
    [JsonPropertyName("agent")]
    public CollabAgent Agent { get; init; } = default!;
}

#endregion
