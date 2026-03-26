using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class TeamService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<TeamService> _logger;
    private ConcurrentDictionary<string, Team> _teams = new();
    private bool _loaded;

    public TeamService(JsonDataStore store, ILogger<TeamService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var teams = await _store.LoadAsync<List<Team>>("teams.json");
        _teams = new ConcurrentDictionary<string, Team>(
            teams.Where(t => !t.IsDeleted).ToDictionary(t => t.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("teams.json", _teams.Values.ToList());
    }

    public async Task<List<Team>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _teams.Values.OrderByDescending(t => t.UpdatedAt).ToList();
    }

    public async Task<Team?> GetByIdAsync(string id)
    {
        await EnsureLoadedAsync();
        return _teams.GetValueOrDefault(id);
    }

    public async Task<Team?> GetByNameAsync(string name)
    {
        await EnsureLoadedAsync();
        return _teams.Values.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Team> CreateAsync(TeamCreateRequest request)
    {
        await EnsureLoadedAsync();
        var team = new Team
        {
            Name = request.Name,
            Description = request.Description,
            ProjectPath = request.ProjectPath,
            Members = request.Members.Select(m => new TeamMember
            {
                Name = m.Name,
                Role = m.Role,
                WorkflowPrompt = m.WorkflowPrompt
            }).ToList()
        };
        _teams[team.Id] = team;
        await PersistAsync();
        _logger.LogInformation("Created team {TeamName} with {MemberCount} members", team.Name, team.Members.Count);
        return team;
    }

    public async Task<Team?> UpdateAsync(string id, Action<Team> update)
    {
        await EnsureLoadedAsync();
        if (!_teams.TryGetValue(id, out var team)) return null;
        update(team);
        team.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
        return team;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureLoadedAsync();
        if (!_teams.TryGetValue(id, out var team)) return false;
        team.IsDeleted = true;
        _teams.TryRemove(id, out _);
        await PersistAsync();
        _logger.LogInformation("Deleted team {TeamName}", team.Name);
        return true;
    }

    public async Task<Team?> LaunchAsync(string teamId, TeamLaunchRequest request)
    {
        var team = await GetByIdAsync(teamId);
        if (team == null) return null;

        team.Status = TeamStatus.Launching;
        team.ProvisioningState = new TeamProvisioningState
        {
            Status = ProvisioningStatus.Spawning,
            Steps = new List<ProvisioningStep>
            {
                new() { Name = "Initializing workspace", Status = "in_progress" },
                new() { Name = "Spawning agents", Status = "pending" },
                new() { Name = "Configuring team protocols", Status = "pending" },
                new() { Name = "Ready", Status = "pending" }
            }
        };
        team.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();

        // Simulate async provisioning
        _ = SimulateProvisioningAsync(team);
        return team;
    }

    private async Task SimulateProvisioningAsync(Team team)
    {
        try
        {
            await Task.Delay(1000);
            if (team.ProvisioningState == null) return;

            team.ProvisioningState.Steps[0].Status = "completed";
            team.ProvisioningState.Steps[1].Status = "in_progress";
            team.ProvisioningState.Status = ProvisioningStatus.Spawning;
            await PersistAsync();

            foreach (var member in team.Members)
            {
                await Task.Delay(500);
                member.Status = MemberStatus.Active;
                member.SpawnedAt = DateTime.UtcNow;
                member.SessionId = Guid.NewGuid().ToString();
            }

            team.ProvisioningState.Steps[1].Status = "completed";
            team.ProvisioningState.Steps[2].Status = "in_progress";
            team.ProvisioningState.Status = ProvisioningStatus.Configuring;
            await PersistAsync();

            await Task.Delay(800);
            team.ProvisioningState.Steps[2].Status = "completed";
            team.ProvisioningState.Steps[3].Status = "completed";
            team.ProvisioningState.Status = ProvisioningStatus.Ready;
            team.ProvisioningState.CompletedAt = DateTime.UtcNow;
            team.Status = TeamStatus.Running;
            team.UpdatedAt = DateTime.UtcNow;
            await PersistAsync();

            _logger.LogInformation("Team {TeamName} is now running with {Count} agents",
                team.Name, team.Members.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision team {TeamName}", team.Name);
            if (team.ProvisioningState != null)
            {
                team.ProvisioningState.Status = ProvisioningStatus.Failed;
                team.ProvisioningState.Error = ex.Message;
            }
            team.Status = TeamStatus.Failed;
            await PersistAsync();
        }
    }

    public async Task<Team?> StopAsync(string teamId)
    {
        var team = await GetByIdAsync(teamId);
        if (team == null) return null;

        team.Status = TeamStatus.Stopping;
        await PersistAsync();

        foreach (var member in team.Members)
        {
            member.Status = MemberStatus.Terminated;
        }

        team.Status = TeamStatus.Stopped;
        team.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();

        _logger.LogInformation("Stopped team {TeamName}", team.Name);
        return team;
    }

    public async Task<List<TeamSummary>> GetSummariesAsync()
    {
        var teams = await GetAllAsync();
        var taskService = _taskService;
        return teams.Select(t => new TeamSummary
        {
            Id = t.Id,
            Name = t.Name,
            Status = t.Status,
            MemberCount = t.Members.Count,
            ActiveMembers = t.Members.Count(m => m.Status == MemberStatus.Active),
            LastActivity = t.UpdatedAt
        }).ToList();
    }

    // Injected lazily to avoid circular dependency
    private TaskService? _taskService;
    public void SetTaskService(TaskService taskService) => _taskService = taskService;
}
