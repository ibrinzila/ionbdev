using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// Aggregates team/task/inbox data into plain dicts for rendering
/// on the dashboard board view.
/// </summary>
public class BoardCollector
{
    private readonly string _dataDir;
    private readonly TeamManager _teamManager;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BoardCollector(string dataDir, TeamManager teamManager)
    {
        _dataDir = dataDir;
        _teamManager = teamManager;
    }

    /// <summary>Collect lightweight summary for all teams (overview screen).</summary>
    public List<TeamOverviewViewModel> CollectOverview()
    {
        var teamsMeta = _teamManager.DiscoverTeams();
        var result = new List<TeamOverviewViewModel>();

        foreach (var meta in teamsMeta)
        {
            var name = meta["name"]?.ToString() ?? "";
            try
            {
                result.Add(CollectTeamSummary(name));
            }
            catch
            {
                result.Add(new TeamOverviewViewModel
                {
                    Name = name,
                    Description = meta.GetValueOrDefault("description")?.ToString() ?? "",
                });
            }
        }

        return result;
    }

    public TeamOverviewViewModel CollectTeamSummary(string teamName)
    {
        var config = _teamManager.GetTeam(teamName)
            ?? throw new InvalidOperationException($"Team '{teamName}' not found");

        var mailbox = new MailboxManager(_dataDir, teamName);
        var store = new TaskStore(_dataDir, teamName);

        var totalInbox = 0;
        var leaderName = "";

        foreach (var member in config.Members)
        {
            var inboxName = TeamManager.InboxNameFor(member);
            totalInbox += mailbox.PeekCount(inboxName);
            if (string.IsNullOrEmpty(leaderName) && member.AgentId == config.LeadAgentId)
                leaderName = member.Name;
        }

        return new TeamOverviewViewModel
        {
            Name = config.Name,
            Description = config.Description,
            Leader = leaderName,
            MemberCount = config.Members.Count,
            TaskCount = store.ListTasks().Count,
            PendingMessages = totalInbox,
        };
    }

    /// <summary>Collect full board data for a single team.</summary>
    public TeamDetailViewModel CollectTeam(string teamName)
    {
        var config = _teamManager.GetTeam(teamName)
            ?? throw new InvalidOperationException($"Team '{teamName}' not found");

        var mailbox = new MailboxManager(_dataDir, teamName);
        var store = new TaskStore(_dataDir, teamName);

        // Find leader
        var leaderName = "";
        foreach (var m in config.Members)
        {
            if (m.AgentId == config.LeadAgentId)
            {
                leaderName = m.Name;
                break;
            }
        }

        // Members with inbox counts
        var members = config.Members.Select(m =>
        {
            var inboxName = TeamManager.InboxNameFor(m);
            return new MemberViewModel
            {
                Name = m.Name,
                AgentId = m.AgentId,
                AgentType = m.AgentType,
                JoinedAt = m.JoinedAt,
                User = m.User,
                InboxName = inboxName,
                InboxCount = mailbox.PeekCount(inboxName),
            };
        }).ToList();

        // Tasks grouped by status
        var allTasks = store.ListTasks();
        var grouped = new Dictionary<string, List<TaskItem>>
        {
            ["pending"] = new(),
            ["in_progress"] = new(),
            ["completed"] = new(),
            ["blocked"] = new(),
        };

        foreach (var t in allTasks)
        {
            if (grouped.ContainsKey(t.Status))
                grouped[t.Status].Add(t);
            else
                grouped.GetValueOrDefault("pending", new()).Add(t);
        }

        var summary = new TaskSummaryViewModel
        {
            Total = allTasks.Count,
            Pending = grouped["pending"].Count,
            InProgress = grouped["in_progress"].Count,
            Completed = grouped["completed"].Count,
            Blocked = grouped["blocked"].Count,
        };

        // Messages from event log
        var messages = new List<TeamMessage>();
        try
        {
            messages = mailbox.GetEventLog(limit: 200);
        }
        catch { }

        // Cost summary
        CostSummary? costData = null;
        try
        {
            var costStore = new CostStore(_dataDir, teamName);
            costData = costStore.Summary();
        }
        catch { }

        return new TeamDetailViewModel
        {
            Team = config,
            LeaderName = leaderName,
            Members = members,
            Tasks = grouped,
            TaskSummary = summary,
            Messages = messages,
            Cost = costData,
        };
    }
}
