using ExpectMvc.Models;

namespace ExpectMvc.Services;

/// <summary>
/// AI agent that generates test plans from code changes.
/// Mirrors @expect/agent — supports Claude and Codex providers.
/// Implements autonomous tool execution (read files, run commands, search code)
/// so the agent can explore the codebase interactively during planning.
/// Supports session resumption via sessionId.
/// </summary>
public interface IAgentService
{
    Task<TestPlan> GeneratePlanAsync(GitDiff diff, AgentProvider provider, string? message = null, string? sessionId = null);
    IAsyncEnumerable<AgentStreamEvent> GeneratePlanStreamAsync(GitDiff diff, AgentProvider provider, string? message = null, string? sessionId = null);
    string? GetLastSessionId();
}

public class AgentStreamEvent
{
    public AgentStreamEventType Type { get; set; }
    public string Data { get; set; } = string.Empty;
}

public enum AgentStreamEventType
{
    ToolCall,
    ToolResult,
    Reasoning,
    Text,
    PlanReady,
    Error
}
