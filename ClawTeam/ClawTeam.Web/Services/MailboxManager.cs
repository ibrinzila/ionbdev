using System.Text;
using System.Text.Json;
using ClawTeam.Web.Models;
using ClawTeam.Web.Transport;

namespace ClawTeam.Web.Services;

/// <summary>
/// Message broker that handles sending, receiving, and broadcasting
/// messages between agents using a pluggable transport layer.
/// </summary>
public class MailboxManager
{
    private readonly string _teamName;
    private readonly string _dataDir;
    private readonly ITransport _transport;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public MailboxManager(string dataDir, string teamName, ITransport? transport = null)
    {
        _dataDir = dataDir;
        _teamName = teamName;
        _transport = transport ?? new FileTransport(dataDir, teamName);
    }

    // ── Send / Receive ───────────────────────────────────────────────

    public void Send(string from, string to, string content,
        string type = "chat", string? requestId = null,
        List<string>? capabilities = null, string? feedback = null,
        string? planId = null, string? summary = null, string? reason = null)
    {
        var msg = new TeamMessage
        {
            Type = type,
            From = from,
            To = to,
            Team = _teamName,
            Content = content,
            RequestId = requestId,
            Capabilities = capabilities,
            Feedback = feedback,
            PlanId = planId,
            Summary = summary,
            Reason = reason,
        };

        var json = JsonSerializer.Serialize(msg, JsonOpts);
        var data = Encoding.UTF8.GetBytes(json);
        _transport.Deliver(to, data);

        // Persist to event log
        LogEvent(msg);
    }

    public void Broadcast(string from, string content, string type = "broadcast",
        List<string>? exclude = null)
    {
        var recipients = _transport.ListRecipients();
        var excludeSet = new HashSet<string>(exclude ?? new());

        foreach (var recipient in recipients)
        {
            if (excludeSet.Contains(recipient)) continue;
            if (recipient == from) continue;

            Send(from, recipient, content, type);
        }
    }

    public List<TeamMessage> Receive(string agentName, int limit = 10)
    {
        var rawMessages = _transport.Fetch(agentName, limit, consume: true);
        var messages = new List<TeamMessage>();

        foreach (var data in rawMessages)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var msg = JsonSerializer.Deserialize<TeamMessage>(json, JsonOpts);
                if (msg != null)
                    messages.Add(msg);
            }
            catch
            {
                // Skip malformed messages (quarantine handled by transport)
            }
        }

        return messages;
    }

    public List<TeamMessage> Peek(string agentName, int limit = 10)
    {
        var rawMessages = _transport.Fetch(agentName, limit, consume: false);
        var messages = new List<TeamMessage>();

        foreach (var data in rawMessages)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var msg = JsonSerializer.Deserialize<TeamMessage>(json, JsonOpts);
                if (msg != null)
                    messages.Add(msg);
            }
            catch { }
        }

        return messages;
    }

    public int PeekCount(string agentName) => _transport.Count(agentName);

    // ── Event Log ────────────────────────────────────────────────────

    private string EventLogDir()
    {
        var dir = Path.Combine(_dataDir, "teams", _teamName, "events");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void LogEvent(TeamMessage msg)
    {
        var dir = EventLogDir();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var filename = $"evt-{ts}-{uid}.json";
        var path = Path.Combine(dir, filename);

        var json = JsonSerializer.Serialize(msg, JsonOpts);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path);
    }

    public List<TeamMessage> GetEventLog(int limit = 200)
    {
        var dir = EventLogDir();
        if (!Directory.Exists(dir)) return new();

        var files = Directory.GetFiles(dir, "evt-*.json")
            .OrderByDescending(f => f)
            .Take(limit)
            .Reverse()
            .ToList();

        var events = new List<TeamMessage>();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var msg = JsonSerializer.Deserialize<TeamMessage>(json, JsonOpts);
                if (msg != null) events.Add(msg);
            }
            catch { }
        }

        return events;
    }
}
