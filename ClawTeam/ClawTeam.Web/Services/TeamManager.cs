using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// Manages team creation, membership, and discovery.
/// State is persisted as JSON files under {DataDir}/teams/{TeamName}/config.json.
/// </summary>
public class TeamManager
{
    private readonly string _dataDir;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TeamManager(string dataDir)
    {
        _dataDir = dataDir;
    }

    private string TeamsRoot() => Path.Combine(_dataDir, "teams");
    private string TeamDir(string teamName) => Path.Combine(TeamsRoot(), teamName);
    private string ConfigPath(string teamName) => Path.Combine(TeamDir(teamName), "config.json");

    // ── Persistence ──────────────────────────────────────────────────

    private TeamConfig? LoadConfig(string teamName)
    {
        var path = ConfigPath(teamName);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TeamConfig>(json, JsonOpts);
        }
        catch { return null; }
    }

    private void SaveConfig(TeamConfig config)
    {
        var dir = TeamDir(config.Name);
        Directory.CreateDirectory(dir);
        var path = ConfigPath(config.Name);
        var json = JsonSerializer.Serialize(config, JsonOpts);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    // ── Team operations ──────────────────────────────────────────────

    public TeamConfig CreateTeam(string name, string description, string leaderName,
        string agentType = "claude", string? user = null)
    {
        var leader = new TeamMember
        {
            Name = leaderName,
            User = user,
            AgentType = agentType,
        };

        var config = new TeamConfig
        {
            Name = name,
            Description = description,
            LeadAgentId = leader.AgentId,
            Members = new List<TeamMember> { leader },
        };

        SaveConfig(config);
        return config;
    }

    public List<Dictionary<string, object>> DiscoverTeams()
    {
        var root = TeamsRoot();
        if (!Directory.Exists(root)) return new();

        var result = new List<Dictionary<string, object>>();
        foreach (var dir in Directory.GetDirectories(root))
        {
            var teamName = Path.GetFileName(dir);
            var config = LoadConfig(teamName);
            if (config == null) continue;

            result.Add(new Dictionary<string, object>
            {
                ["name"] = config.Name,
                ["description"] = config.Description,
                ["memberCount"] = config.Members.Count,
            });
        }
        return result;
    }

    public TeamConfig? GetTeam(string teamName) => LoadConfig(teamName);

    public TeamMember AddMember(string teamName, string memberName,
        string agentType = "claude", string? user = null)
    {
        var config = LoadConfig(teamName)
            ?? throw new InvalidOperationException($"Team '{teamName}' not found");

        var member = new TeamMember
        {
            Name = memberName,
            User = user,
            AgentType = agentType,
        };

        config.Members.Add(member);
        SaveConfig(config);
        return member;
    }

    public bool RemoveMember(string teamName, string memberName)
    {
        var config = LoadConfig(teamName);
        if (config == null) return false;

        var removed = config.Members.RemoveAll(m => m.Name == memberName) > 0;
        if (removed) SaveConfig(config);
        return removed;
    }

    public TeamMember? GetMember(string teamName, string memberName, string? user = null)
    {
        var config = LoadConfig(teamName);
        return config?.Members.FirstOrDefault(m =>
            m.Name == memberName && (user == null || m.User == user));
    }

    public List<TeamMember> ListMembers(string teamName)
    {
        var config = LoadConfig(teamName);
        return config?.Members ?? new();
    }

    public string? GetLeaderName(string teamName)
    {
        var config = LoadConfig(teamName);
        if (config == null) return null;
        return config.Members
            .FirstOrDefault(m => m.AgentId == config.LeadAgentId)?.Name;
    }

    /// <summary>Compute the inbox name for a member (user_name or just name).</summary>
    public static string InboxNameFor(TeamMember member)
    {
        return string.IsNullOrEmpty(member.User) ? member.Name : $"{member.User}_{member.Name}";
    }

    /// <summary>Best-effort cleanup of team directories.</summary>
    public void Cleanup(string teamName, bool force = false)
    {
        var teamDir = TeamDir(teamName);
        if (Directory.Exists(teamDir))
        {
            try { Directory.Delete(teamDir, true); } catch { }
        }

        // Also clean up tasks, costs, sessions dirs
        foreach (var sub in new[] { "tasks", "costs", "sessions" })
        {
            var subDir = Path.Combine(_dataDir, sub, teamName);
            if (Directory.Exists(subDir))
            {
                try { Directory.Delete(subDir, true); } catch { }
            }
        }
    }
}
