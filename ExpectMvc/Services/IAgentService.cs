using ExpectMvc.Models;

namespace ExpectMvc.Services;

/// <summary>
/// AI agent that generates test plans from code changes.
/// Mirrors @expect/agent — supports Claude and Codex providers.
/// </summary>
public interface IAgentService
{
    Task<TestPlan> GeneratePlanAsync(GitDiff diff, AgentProvider provider, string? message = null);
}
