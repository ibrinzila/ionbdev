using System.Text.Json;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services.Providers;

/// <summary>
/// Provider for Factory.ai Droid CLI sessions. Reads JSONL files from ~/.factory/sessions/.
/// </summary>
public class DroidCliProvider : IAgentProvider
{
    public string Id => "droid-cli";
    public string Name => "Droid CLI";

    private string GetSessionsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".factory", "sessions");
    }

    public ProviderCheckResult Check(string? customCommand = null)
    {
        var cmd = customCommand ?? "droid";
        return new ProviderCheckResult
        {
            ProviderId = Id,
            ProviderName = Name,
            IsInstalled = FindExecutable(cmd) != null,
            Location = FindExecutable(cmd)
        };
    }

    public bool DetectAgent(string projectPath)
    {
        var dir = GetSessionsDir();
        return Directory.Exists(dir) && Directory.GetFiles(dir, "*.jsonl").Length > 0;
    }

    public async Task<SessionData?> GetSessionAsync(string projectPath, string sessionId,
        bool debugRaw = false)
    {
        var filePath = Path.Combine(GetSessionsDir(), sessionId + ".jsonl");
        if (!File.Exists(filePath))
            return null;

        return await ParseSessionFileAsync(filePath, sessionId);
    }

    public async Task<List<SessionData>> GetSessionsAsync(string projectPath, bool debugRaw = false,
        IProgress<int>? progress = null)
    {
        var sessions = new List<SessionData>();
        var dir = GetSessionsDir();
        if (!Directory.Exists(dir)) return sessions;

        var count = 0;
        foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
        {
            try
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var session = await ParseSessionFileAsync(file, id);
                if (session != null)
                {
                    sessions.Add(session);
                    count++;
                    progress?.Report(count);
                }
            }
            catch { }
        }

        return sessions;
    }

    public Task<List<SessionMetadata>> ListSessionsAsync(string projectPath)
    {
        var metadata = new List<SessionMetadata>();
        var dir = GetSessionsDir();
        if (!Directory.Exists(dir)) return Task.FromResult(metadata);

        foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
        {
            var info = new FileInfo(file);
            metadata.Add(new SessionMetadata
            {
                SessionId = Path.GetFileNameWithoutExtension(file),
                ProviderId = Id,
                ProviderName = Name,
                CreatedAt = info.CreationTimeUtc.ToString("o"),
                FilePath = file
            });
        }

        return Task.FromResult(metadata);
    }

    private async Task<SessionData?> ParseSessionFileAsync(string filePath, string sessionId)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length == 0) return null;

        var session = new SessionData
        {
            SchemaVersion = "1",
            Provider = new ProviderInfo { Id = Id, Name = Name },
            SessionId = sessionId
        };

        var currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var role = GetString(root, "role") ?? GetString(root, "type") ?? "unknown";
                var message = new Message
                {
                    Id = GetString(root, "id") ?? Guid.NewGuid().ToString(),
                    Role = role switch
                    {
                        "human" or "user" => "user",
                        _ => "agent"
                    },
                    Timestamp = GetString(root, "timestamp"),
                    Model = GetString(root, "model")
                };

                if (root.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        message.Content.Add(new ContentPart
                        {
                            Type = "text",
                            Text = content.GetString() ?? ""
                        });
                    }
                    else if (content.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var part in content.EnumerateArray())
                        {
                            message.Content.Add(new ContentPart
                            {
                                Type = GetString(part, "type") ?? "text",
                                Text = GetString(part, "text") ?? ""
                            });
                        }
                    }
                }

                if (root.TryGetProperty("usage", out var usage))
                {
                    message.Usage = new Usage
                    {
                        InputTokens = GetInt(usage, "input_tokens"),
                        OutputTokens = GetInt(usage, "output_tokens"),
                        CacheCreationInputTokens = GetNullableInt(usage, "cache_creation_input_tokens"),
                        CacheReadInputTokens = GetNullableInt(usage, "cache_read_input_tokens")
                    };
                }

                if (message.Role == "user" && currentExchange.Messages.Count > 0)
                {
                    session.Exchanges.Add(currentExchange);
                    currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };
                }

                currentExchange.Messages.Add(message);

                if (session.CreatedAt == null && message.Timestamp != null)
                    session.CreatedAt = message.Timestamp;
                if (message.Timestamp != null)
                    session.UpdatedAt = message.Timestamp;
            }
            catch { }
        }

        if (currentExchange.Messages.Count > 0)
            session.Exchanges.Add(currentExchange);

        return session;
    }

    private static string? FindExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathEnv.Split(Path.PathSeparator))
        {
            var full = Path.Combine(p, name);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string? GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int GetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

    private static int? GetNullableInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
}
