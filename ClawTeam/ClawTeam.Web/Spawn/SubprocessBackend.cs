using System.Diagnostics;

namespace ClawTeam.Web.Spawn;

/// <summary>
/// Spawns agents as local subprocesses.
/// </summary>
public class SubprocessBackend : ISpawnBackend
{
    private readonly List<(Process Process, RunningAgent Agent)> _running = new();
    private readonly object _lock = new();

    public string Spawn(SpawnRequest request)
    {
        if (request.Command.Count == 0)
            throw new ArgumentException("Command list cannot be empty");

        var psi = new ProcessStartInfo
        {
            FileName = request.Command[0],
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Add arguments
        for (var i = 1; i < request.Command.Count; i++)
            psi.ArgumentList.Add(request.Command[i]);

        // Add prompt as final argument if present
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(request.Prompt);
        }

        if (request.Cwd != null)
            psi.WorkingDirectory = request.Cwd;

        // Set environment variables
        if (request.Env != null)
        {
            foreach (var (key, value) in request.Env)
                psi.Environment[key] = value;
        }

        psi.Environment["CLAWTEAM_TEAM"] = request.TeamName;
        psi.Environment["CLAWTEAM_AGENT_NAME"] = request.AgentName;
        psi.Environment["CLAWTEAM_AGENT_ID"] = request.AgentId;

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {request.Command[0]}");

        var agent = new RunningAgent
        {
            Name = request.AgentName,
            Id = request.AgentId,
            Status = "running",
            ProcessId = process.Id,
        };

        lock (_lock)
        {
            _running.Add((process, agent));
        }

        return $"Agent '{request.AgentName}' spawned as PID {process.Id}";
    }

    public List<RunningAgent> ListRunning()
    {
        lock (_lock)
        {
            // Clean up exited processes
            _running.RemoveAll(entry =>
            {
                try { return entry.Process.HasExited; }
                catch { return true; }
            });

            return _running.Select(entry => entry.Agent).ToList();
        }
    }
}
