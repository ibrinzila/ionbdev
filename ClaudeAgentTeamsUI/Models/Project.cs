namespace ClaudeAgentTeamsUI.Models;

public class Project
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? GitRemote { get; set; }
    public string? GitBranch { get; set; }
    public int SessionCount { get; set; }
    public DateTime? LastActivity { get; set; }
    public List<string> WorktreeIds { get; set; } = new();
}

public class RepositoryGroup
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string? RemoteUrl { get; set; }
    public List<Project> Projects { get; set; } = new();
}
