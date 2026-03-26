using System.Diagnostics;
using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// Manages git worktree-based workspaces for agent isolation.
/// Each agent gets its own branch: clawteam/{team}/{agent}.
/// </summary>
public class WorkspaceManager
{
    private readonly string _dataDir;
    private readonly string _teamName;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public WorkspaceManager(string dataDir, string teamName)
    {
        _dataDir = dataDir;
        _teamName = teamName;
    }

    private string RegistryPath() =>
        Path.Combine(_dataDir, "workspaces", _teamName, "registry.json");

    private WorkspaceRegistry LoadRegistry()
    {
        var path = RegistryPath();
        if (!File.Exists(path))
            return new WorkspaceRegistry { TeamName = _teamName };

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WorkspaceRegistry>(json, JsonOpts)
                ?? new WorkspaceRegistry { TeamName = _teamName };
        }
        catch
        {
            return new WorkspaceRegistry { TeamName = _teamName };
        }
    }

    private void SaveRegistry(WorkspaceRegistry registry)
    {
        var dir = Path.GetDirectoryName(RegistryPath())!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(registry, JsonOpts);
        var temp = RegistryPath() + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, RegistryPath(), overwrite: true);
    }

    /// <summary>Create a new worktree for an agent.</summary>
    public WorkspaceInfo? CreateWorkspace(string agentName, string agentId,
        string repoRoot, string? baseBranch = null)
    {
        var branchName = $"clawteam/{_teamName}/{agentName}";
        var worktreePath = Path.Combine(_dataDir, "workspaces", _teamName, agentName);

        try
        {
            // Create branch and worktree
            baseBranch ??= GetCurrentBranch(repoRoot) ?? "main";
            RunGit(repoRoot, $"branch {branchName} {baseBranch}");
            RunGit(repoRoot, $"worktree add \"{worktreePath}\" {branchName}");
        }
        catch
        {
            return null;
        }

        var info = new WorkspaceInfo
        {
            AgentName = agentName,
            AgentId = agentId,
            TeamName = _teamName,
            BranchName = branchName,
            WorktreePath = worktreePath,
            RepoRoot = repoRoot,
            BaseBranch = baseBranch ?? "main",
        };

        var registry = LoadRegistry();
        registry.RepoRoot = repoRoot;
        registry.Workspaces.Add(info);
        SaveRegistry(registry);

        return info;
    }

    public List<WorkspaceInfo> ListWorkspaces()
    {
        return LoadRegistry().Workspaces;
    }

    /// <summary>Checkpoint: commit current work in an agent's worktree.</summary>
    public bool Checkpoint(string agentName, string? message = null)
    {
        var workspace = LoadRegistry().Workspaces
            .FirstOrDefault(w => w.AgentName == agentName);
        if (workspace == null) return false;

        try
        {
            RunGit(workspace.WorktreePath, "add -A");
            RunGit(workspace.WorktreePath,
                $"commit -m \"{message ?? $"checkpoint: {agentName}"}\"");
            return true;
        }
        catch { return false; }
    }

    /// <summary>Merge an agent's branch back to the base branch.</summary>
    public bool Merge(string agentName)
    {
        var workspace = LoadRegistry().Workspaces
            .FirstOrDefault(w => w.AgentName == agentName);
        if (workspace == null) return false;

        try
        {
            RunGit(workspace.RepoRoot,
                $"merge {workspace.BranchName} --no-ff -m \"Merge {workspace.BranchName}\"");
            return true;
        }
        catch { return false; }
    }

    /// <summary>Clean up an agent's worktree and branch.</summary>
    public bool CleanupWorkspace(string agentName)
    {
        var registry = LoadRegistry();
        var workspace = registry.Workspaces.FirstOrDefault(w => w.AgentName == agentName);
        if (workspace == null) return false;

        try
        {
            RunGit(workspace.RepoRoot, $"worktree remove \"{workspace.WorktreePath}\" --force");
        }
        catch { }

        try
        {
            RunGit(workspace.RepoRoot, $"branch -D {workspace.BranchName}");
        }
        catch { }

        registry.Workspaces.Remove(workspace);
        SaveRegistry(registry);
        return true;
    }

    // ── Git helpers ──────────────────────────────────────────────────

    private static string? GetCurrentBranch(string repoRoot)
    {
        try
        {
            return RunGit(repoRoot, "rev-parse --abbrev-ref HEAD").Trim();
        }
        catch { return null; }
    }

    private static string RunGit(string workDir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(30_000);

        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {args} failed: {err}");
        }

        return output;
    }
}
