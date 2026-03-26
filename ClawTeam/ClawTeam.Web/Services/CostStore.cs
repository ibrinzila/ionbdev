using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// File-based cost tracking. Events stored as
/// {DataDir}/costs/{Team}/cost-{ts}-{uid}.json.
/// </summary>
public class CostStore
{
    private readonly string _teamName;
    private readonly string _dataDir;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public CostStore(string dataDir, string teamName)
    {
        _dataDir = dataDir;
        _teamName = teamName;
    }

    private string CostsDir()
    {
        var dir = Path.Combine(_dataDir, "costs", _teamName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Report(CostEvent evt)
    {
        var dir = CostsDir();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var filename = $"cost-{ts}-{uid}.json";
        var path = Path.Combine(dir, filename);

        var json = JsonSerializer.Serialize(evt, JsonOpts);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path);
    }

    public List<CostEvent> ListEvents(string? agentName = null)
    {
        var dir = CostsDir();
        if (!Directory.Exists(dir)) return new();

        var files = Directory.GetFiles(dir, "cost-*.json");
        var events = new List<CostEvent>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var evt = JsonSerializer.Deserialize<CostEvent>(json, JsonOpts);
                if (evt == null) continue;
                if (agentName != null && evt.AgentName != agentName) continue;
                events.Add(evt);
            }
            catch { }
        }

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    public CostSummary Summary()
    {
        var events = ListEvents();
        var summary = new CostSummary
        {
            EventCount = events.Count,
        };

        foreach (var evt in events)
        {
            summary.TotalCostCents += evt.CostCents;
            summary.TotalInputTokens += evt.InputTokens;
            summary.TotalOutputTokens += evt.OutputTokens;

            if (!summary.ByAgent.ContainsKey(evt.AgentName))
            {
                summary.ByAgent[evt.AgentName] = new AgentCostSummary();
            }

            var agent = summary.ByAgent[evt.AgentName];
            agent.CostCents += evt.CostCents;
            agent.InputTokens += evt.InputTokens;
            agent.OutputTokens += evt.OutputTokens;
            agent.EventCount++;
        }

        return summary;
    }
}
