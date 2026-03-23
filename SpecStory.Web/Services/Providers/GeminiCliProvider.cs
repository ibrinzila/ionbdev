using System.Text.Json;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services.Providers;

/// <summary>
/// Provider for Gemini CLI sessions. Reads JSON files from ~/.gemini/tmp/.
/// </summary>
public class GeminiCliProvider : IAgentProvider
{
    public string Id => "gemini-cli";
    public string Name => "Gemini CLI";

    private string GetSessionsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".gemini", "tmp");
    }

    public ProviderCheckResult Check(string? customCommand = null)
    {
        var cmd = customCommand ?? "gemini";
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
        return Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0;
    }

    public async Task<SessionData?> GetSessionAsync(string projectPath, string sessionId,
        bool debugRaw = false)
    {
        var filePath = Path.Combine(GetSessionsDir(), sessionId + ".json");
        if (!File.Exists(filePath))
            return null;

        return await ParseJsonSessionAsync(filePath, sessionId);
    }

    public async Task<List<SessionData>> GetSessionsAsync(string projectPath, bool debugRaw = false,
        IProgress<int>? progress = null)
    {
        var sessions = new List<SessionData>();
        var dir = GetSessionsDir();
        if (!Directory.Exists(dir)) return sessions;

        var count = 0;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var session = await ParseJsonSessionAsync(file, id);
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

        foreach (var file in Directory.GetFiles(dir, "*.json"))
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

    private async Task<SessionData?> ParseJsonSessionAsync(string filePath, string sessionId)
    {
        var json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var session = new SessionData
        {
            SchemaVersion = "1",
            Provider = new ProviderInfo { Id = Id, Name = Name },
            SessionId = sessionId
        };

        // Gemini stores sessions as a JSON array of messages
        if (root.ValueKind == JsonValueKind.Array)
        {
            var currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };

            foreach (var item in root.EnumerateArray())
            {
                var role = GetString(item, "role") ?? "unknown";
                var message = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = role == "user" ? "user" : "agent",
                    Model = GetString(item, "model")
                };

                if (item.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text))
                        {
                            message.Content.Add(new ContentPart
                            {
                                Type = GetString(part, "thought") == "true" ? "thinking" : "text",
                                Text = text.GetString() ?? ""
                            });
                        }
                    }
                }

                if (item.TryGetProperty("usageMetadata", out var usage))
                {
                    message.Usage = new Usage
                    {
                        InputTokens = GetInt(usage, "promptTokenCount"),
                        OutputTokens = GetInt(usage, "candidatesTokenCount"),
                        CachedInputTokens = GetNullableInt(usage, "cachedContentTokenCount"),
                        ThoughtTokens = GetNullableInt(usage, "thoughtsTokenCount"),
                        ToolTokens = GetNullableInt(usage, "toolUsePromptTokenCount")
                    };
                }

                if (message.Role == "user" && currentExchange.Messages.Count > 0)
                {
                    session.Exchanges.Add(currentExchange);
                    currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };
                }

                currentExchange.Messages.Add(message);
            }

            if (currentExchange.Messages.Count > 0)
                session.Exchanges.Add(currentExchange);
        }

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
