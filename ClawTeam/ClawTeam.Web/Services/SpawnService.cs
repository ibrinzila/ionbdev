using ClawTeam.Web.Spawn;

namespace ClawTeam.Web.Services;

/// <summary>
/// High-level service for spawning agents into a team.
/// Registers the member, optionally creates a workspace, and spawns the process.
/// </summary>
public class SpawnService
{
    private readonly string _dataDir;
    private readonly TeamManager _teamManager;
    private readonly ISpawnBackend _backend;
    private readonly NativeCliAdapter _adapter = new();

    public SpawnService(string dataDir, TeamManager teamManager, ISpawnBackend? backend = null)
    {
        _dataDir = dataDir;
        _teamManager = teamManager;
        _backend = backend ?? new SubprocessBackend();
    }

    public string SpawnAgent(string teamName, string agentName, string agentType = "claude",
        string? task = null, string? command = null, bool skipPermissions = true,
        string? cwd = null, string? user = null)
    {
        // 1. Add member to team
        var member = _teamManager.AddMember(teamName, agentName, agentType, user);

        // 2. Determine command to run
        var cmd = new List<string>();
        if (!string.IsNullOrEmpty(command))
        {
            cmd.AddRange(command.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
        else
        {
            cmd.Add(agentType); // e.g., "claude", "codex"
        }

        // 3. Prepare command with adapter
        var prepared = _adapter.PrepareCommand(
            cmd,
            prompt: task,
            cwd: cwd,
            skipPermissions: skipPermissions,
            agentName: agentName
        );

        // 4. Build coordination prompt
        var coordPrompt = BuildCoordinationPrompt(teamName, agentName, task);

        // 5. Spawn the process
        var request = new SpawnRequest
        {
            Command = prepared.FinalCommand,
            AgentName = agentName,
            AgentId = member.AgentId,
            AgentType = agentType,
            TeamName = teamName,
            Prompt = coordPrompt,
            Cwd = cwd,
            SkipPermissions = skipPermissions,
        };

        return _backend.Spawn(request);
    }

    public List<RunningAgent> ListRunning() => _backend.ListRunning();

    private static string BuildCoordinationPrompt(string teamName, string agentName, string? task)
    {
        var lines = new List<string>
        {
            $"You are agent '{agentName}' on team '{teamName}'.",
            "",
            "== ClawTeam Coordination Commands ==",
            $"• Check tasks:   clawteam task list {teamName} --owner {agentName}",
            $"• Start task:    clawteam task update {teamName} <id> --status in_progress",
            $"• Finish task:   clawteam task update {teamName} <id> --status completed",
            $"• Message leader: clawteam inbox send {teamName} leader \"status...\"",
            $"• Check inbox:   clawteam inbox receive {teamName}",
            $"• Report idle:   clawteam lifecycle idle {teamName}",
        };

        if (!string.IsNullOrEmpty(task))
        {
            lines.Add("");
            lines.Add($"Your task: {task}");
        }

        return string.Join("\n", lines);
    }
}
