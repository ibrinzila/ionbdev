using System.Collections.Concurrent;
using System.Text.Json;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class SessionService
{
    private readonly ILogger<SessionService> _logger;
    private readonly string _claudeDir;
    private readonly ConcurrentDictionary<string, Session> _sessionCache = new();

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
        _claudeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
    }

    public async Task<List<SessionListItem>> GetSessionListAsync(string? projectId = null, int page = 1, int pageSize = 25)
    {
        var sessions = await ScanSessionsAsync();
        var query = sessions.AsEnumerable();

        if (!string.IsNullOrEmpty(projectId))
            query = query.Where(s => s.ProjectId == projectId);

        return query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SessionListItem
            {
                Id = s.Id,
                ProjectName = s.ProjectName,
                StartedAt = s.StartedAt,
                Duration = s.Metrics.Duration,
                TotalTokens = s.Metrics.TotalTokens,
                ToolCalls = s.Metrics.ToolCalls,
                EstimatedCostUsd = s.Metrics.EstimatedCostUsd,
                IsPinned = s.IsPinned
            })
            .ToList();
    }

    public async Task<int> GetTotalCountAsync(string? projectId = null)
    {
        var sessions = await ScanSessionsAsync();
        if (!string.IsNullOrEmpty(projectId))
            return sessions.Count(s => s.ProjectId == projectId);
        return sessions.Count;
    }

    public async Task<Session?> GetSessionAsync(string sessionId)
    {
        if (_sessionCache.TryGetValue(sessionId, out var cached))
            return cached;

        var sessions = await ScanSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public async Task<List<WaterfallItem>> GetWaterfallAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null) return new();

        // Generate waterfall from conversation groups
        var items = new List<WaterfallItem>();
        var offset = TimeSpan.Zero;
        foreach (var group in session.ConversationGroups)
        {
            var duration = TimeSpan.FromSeconds(2); // Estimated
            items.Add(new WaterfallItem
            {
                Label = group.Role == "assistant" ? "Response" : "User",
                Category = group.Role,
                Start = session.StartedAt + offset,
                End = session.StartedAt + offset + duration,
                Tokens = group.Content.Length / 4 // Rough estimate
            });
            foreach (var tc in group.ToolCalls)
            {
                items.Add(new WaterfallItem
                {
                    Label = tc.ToolName,
                    Category = "tool",
                    Start = session.StartedAt + offset,
                    End = session.StartedAt + offset + (tc.Duration ?? TimeSpan.FromSeconds(1)),
                    Tokens = (tc.Output?.Length ?? 0) / 4
                });
            }
            offset += duration;
        }
        return items;
    }

    private async Task<List<Session>> ScanSessionsAsync()
    {
        if (_sessionCache.Any()) return _sessionCache.Values.ToList();

        var sessions = new List<Session>();
        var projectsDir = Path.Combine(_claudeDir, "projects");
        if (!Directory.Exists(projectsDir))
        {
            // Generate demo sessions for testing
            sessions = GenerateDemoSessions();
            foreach (var s in sessions) _sessionCache[s.Id] = s;
            return sessions;
        }

        try
        {
            foreach (var projectDir in Directory.GetDirectories(projectsDir))
            {
                var projectName = Path.GetFileName(projectDir);
                foreach (var sessionFile in Directory.GetFiles(projectDir, "*.jsonl"))
                {
                    try
                    {
                        var session = await ParseSessionFileAsync(sessionFile, projectName);
                        if (session != null)
                        {
                            sessions.Add(session);
                            _sessionCache[session.Id] = session;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse session file {File}", sessionFile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan sessions directory");
        }

        if (!sessions.Any())
        {
            sessions = GenerateDemoSessions();
            foreach (var s in sessions) _sessionCache[s.Id] = s;
        }

        return sessions;
    }

    private async Task<Session?> ParseSessionFileAsync(string filePath, string projectName)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length == 0) return null;

        var sessionId = Path.GetFileNameWithoutExtension(filePath);
        var session = new Session
        {
            Id = sessionId,
            ProjectId = projectName,
            ProjectName = projectName,
            WorkingDirectory = filePath,
            StartedAt = File.GetCreationTimeUtc(filePath)
        };

        int totalTokens = 0, toolCalls = 0, messageCount = 0;

        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var role = root.TryGetProperty("role", out var r) ? r.GetString() : null;
                var content = root.TryGetProperty("content", out var c) ? c.ToString() : "";

                messageCount++;
                totalTokens += content.Length / 4;

                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("input_tokens", out var inp))
                        session.Metrics.InputTokens += inp.GetInt32();
                    if (usage.TryGetProperty("output_tokens", out var outp))
                        session.Metrics.OutputTokens += outp.GetInt32();
                }

                session.ConversationGroups.Add(new ConversationGroup
                {
                    Role = role ?? "unknown",
                    Content = content.Length > 500 ? content[..500] : content,
                    Timestamp = session.StartedAt.AddSeconds(messageCount * 2)
                });
            }
            catch { /* Skip malformed lines */ }
        }

        session.Metrics.TotalTokens = totalTokens;
        session.Metrics.ToolCalls = toolCalls;
        session.Metrics.MessageCount = messageCount;
        session.Metrics.Duration = TimeSpan.FromSeconds(messageCount * 3);
        session.Metrics.EstimatedCostUsd = totalTokens * 0.000003m;
        session.EndedAt = session.StartedAt + session.Metrics.Duration;

        return session;
    }

    private List<Session> GenerateDemoSessions()
    {
        var sessions = new List<Session>();
        var rng = new Random(42);
        var projects = new[] { "web-app", "api-service", "mobile-client", "data-pipeline", "infra-config" };

        for (int i = 0; i < 15; i++)
        {
            var project = projects[rng.Next(projects.Length)];
            var startedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 168));
            var duration = TimeSpan.FromMinutes(rng.Next(2, 45));
            var tokens = rng.Next(5000, 200000);

            sessions.Add(new Session
            {
                Id = $"session-{i:D4}",
                ProjectId = project,
                ProjectName = project,
                StartedAt = startedAt,
                EndedAt = startedAt + duration,
                Metrics = new SessionMetrics
                {
                    TotalTokens = tokens,
                    InputTokens = tokens / 3,
                    OutputTokens = tokens / 3,
                    ThinkingTokens = tokens / 6,
                    CacheReadTokens = tokens / 6,
                    ToolCalls = rng.Next(5, 80),
                    MessageCount = rng.Next(10, 100),
                    Duration = duration,
                    EstimatedCostUsd = tokens * 0.000003m,
                    TokensByCategory = new Dictionary<string, int>
                    {
                        ["user_messages"] = tokens / 6,
                        ["claude_md"] = tokens / 12,
                        ["tool_outputs"] = tokens / 4,
                        ["thinking"] = tokens / 6,
                        ["team_coordination"] = tokens / 12,
                        ["session_cost"] = tokens / 6
                    }
                },
                ConversationGroups = Enumerable.Range(0, rng.Next(5, 20)).Select(j => new ConversationGroup
                {
                    Role = j % 2 == 0 ? "user" : "assistant",
                    Content = j % 2 == 0
                        ? $"Implement feature #{j} for the {project} project"
                        : $"I'll implement feature #{j}. Let me start by analyzing the codebase...",
                    Timestamp = startedAt.AddMinutes(j * 2),
                    ToolCalls = j % 2 == 1 ? new List<ToolCall>
                    {
                        new() { ToolName = "Read", Input = "src/main.ts", Duration = TimeSpan.FromMilliseconds(rng.Next(50, 500)) },
                        new() { ToolName = "Edit", Input = "src/feature.ts", Duration = TimeSpan.FromMilliseconds(rng.Next(100, 800)) }
                    } : new()
                }).ToList()
            });
        }
        return sessions;
    }
}
