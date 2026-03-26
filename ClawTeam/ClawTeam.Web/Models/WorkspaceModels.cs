using System.Text.Json.Serialization;

namespace ClawTeam.Web.Models;

/// <summary>Information about a single agent workspace (git worktree).</summary>
public class WorkspaceInfo
{
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("branch_name")]
    public string BranchName { get; set; } = string.Empty;

    [JsonPropertyName("worktree_path")]
    public string WorktreePath { get; set; } = string.Empty;

    [JsonPropertyName("repo_root")]
    public string RepoRoot { get; set; } = string.Empty;

    [JsonPropertyName("base_branch")]
    public string BaseBranch { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}

/// <summary>Tracks all active workspaces for a team.</summary>
public class WorkspaceRegistry
{
    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("repo_root")]
    public string RepoRoot { get; set; } = string.Empty;

    [JsonPropertyName("workspaces")]
    public List<WorkspaceInfo> Workspaces { get; set; } = new();
}
