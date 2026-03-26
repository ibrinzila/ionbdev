namespace ClaudeAgentTeamsUI.Models;

public class Session
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? WorkingDirectory { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public SessionMetrics Metrics { get; set; } = new();
    public List<ConversationGroup> ConversationGroups { get; set; } = new();
    public bool IsPinned { get; set; }
    public bool IsHidden { get; set; }
}

public class SessionMetrics
{
    public int TotalTokens { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheCreationTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int ThinkingTokens { get; set; }
    public int ToolCalls { get; set; }
    public int MessageCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, int> TokensByCategory { get; set; } = new()
    {
        ["user_messages"] = 0,
        ["claude_md"] = 0,
        ["tool_outputs"] = 0,
        ["thinking"] = 0,
        ["team_coordination"] = 0,
        ["session_cost"] = 0
    };
}

public class ConversationGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = new();
}

public class ToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToolName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string? Output { get; set; }
    public TimeSpan? Duration { get; set; }
}

public class WaterfallItem
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Tokens { get; set; }
}

public class SessionListItem
{
    public string Id { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalTokens { get; set; }
    public int ToolCalls { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool IsPinned { get; set; }
}
