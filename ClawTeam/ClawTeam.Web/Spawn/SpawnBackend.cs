namespace ClawTeam.Web.Spawn;

/// <summary>
/// Base interface for different ways to spawn team agents.
/// </summary>
public interface ISpawnBackend
{
    /// <summary>Spawn a new agent process. Returns a status message.</summary>
    string Spawn(SpawnRequest request);

    /// <summary>List currently running agents.</summary>
    List<RunningAgent> ListRunning();
}

public class SpawnRequest
{
    public List<string> Command { get; set; } = new();
    public string AgentName { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentType { get; set; } = "claude";
    public string TeamName { get; set; } = string.Empty;
    public string? Prompt { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public string? Cwd { get; set; }
    public bool SkipPermissions { get; set; }
    public string? SystemPrompt { get; set; }
}

public class RunningAgent
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "running";
    public int? ProcessId { get; set; }
}
