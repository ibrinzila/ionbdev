using System.Text.Json;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services.Providers;

/// <summary>
/// Provider for Claude Code sessions. Reads JSONL files from ~/.claude/projects/.
/// </summary>
public class ClaudeCodeProvider : IAgentProvider
{
    public string Id => "claude-code";
    public string Name => "Claude Code";

    private string GetSessionsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", "projects");
    }

    public ProviderCheckResult Check(string? customCommand = null)
    {
        var cmd = customCommand ?? "claude";
        var result = new ProviderCheckResult
        {
            ProviderId = Id,
            ProviderName = Name
        };

        try
        {
            var which = FindExecutable(cmd);
            result.IsInstalled = which != null;
            result.Location = which;
            if (which != null)
                result.Version = GetVersion(cmd);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    public bool DetectAgent(string projectPath)
    {
        var sessionsDir = GetSessionsDir();
        if (!Directory.Exists(sessionsDir))
            return false;

        // Look for project-specific session directories
        return Directory.EnumerateDirectories(sessionsDir)
            .Any(d => HasSessionFiles(d));
    }

    public async Task<SessionData?> GetSessionAsync(string projectPath, string sessionId,
        bool debugRaw = false)
    {
        var sessionsDir = GetSessionsDir();
        if (!Directory.Exists(sessionsDir))
            return null;

        foreach (var projectDir in Directory.GetDirectories(sessionsDir))
        {
            var sessionFile = FindSessionFile(projectDir, sessionId);
            if (sessionFile != null)
            {
                return await ParseJsonlSessionAsync(sessionFile, sessionId);
            }
        }

        return null;
    }

    public async Task<List<SessionData>> GetSessionsAsync(string projectPath, bool debugRaw = false,
        IProgress<int>? progress = null)
    {
        var sessions = new List<SessionData>();
        var sessionsDir = GetSessionsDir();

        if (!Directory.Exists(sessionsDir))
            return sessions;

        var count = 0;
        foreach (var projectDir in Directory.GetDirectories(sessionsDir))
        {
            var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
            foreach (var file in jsonlFiles)
            {
                try
                {
                    var sessionId = Path.GetFileNameWithoutExtension(file);
                    var session = await ParseJsonlSessionAsync(file, sessionId);
                    if (session != null)
                    {
                        sessions.Add(session);
                        count++;
                        progress?.Report(count);
                    }
                }
                catch
                {
                    // Skip malformed session files
                }
            }
        }

        return sessions;
    }

    public Task<List<SessionMetadata>> ListSessionsAsync(string projectPath)
    {
        var metadata = new List<SessionMetadata>();
        var sessionsDir = GetSessionsDir();

        if (!Directory.Exists(sessionsDir))
            return Task.FromResult(metadata);

        foreach (var projectDir in Directory.GetDirectories(sessionsDir))
        {
            var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
            foreach (var file in jsonlFiles)
            {
                var info = new FileInfo(file);
                metadata.Add(new SessionMetadata
                {
                    SessionId = Path.GetFileNameWithoutExtension(file),
                    ProviderId = Id,
                    ProviderName = Name,
                    CreatedAt = info.CreationTimeUtc.ToString("o"),
                    WorkspaceRoot = DecodeProjectPath(Path.GetFileName(projectDir)),
                    FilePath = file
                });
            }
        }

        return Task.FromResult(metadata);
    }

    private async Task<SessionData?> ParseJsonlSessionAsync(string filePath, string sessionId)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length == 0)
            return null;

        var session = new SessionData
        {
            SchemaVersion = "1",
            Provider = new ProviderInfo { Id = Id, Name = Name },
            SessionId = sessionId,
            WorkspaceRoot = DecodeProjectPath(
                Path.GetFileName(Path.GetDirectoryName(filePath) ?? ""))
        };

        var currentExchange = new Exchange
        {
            ExchangeId = Guid.NewGuid().ToString()
        };

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var message = ParseJsonlMessage(root);
                if (message == null)
                    continue;

                // Start a new exchange on each user message
                if (message.Role == "user" && currentExchange.Messages.Count > 0)
                {
                    session.Exchanges.Add(currentExchange);
                    currentExchange = new Exchange
                    {
                        ExchangeId = Guid.NewGuid().ToString()
                    };
                }

                currentExchange.Messages.Add(message);

                if (session.CreatedAt == null && message.Timestamp != null)
                    session.CreatedAt = message.Timestamp;

                if (message.Timestamp != null)
                    session.UpdatedAt = message.Timestamp;
            }
            catch
            {
                // Skip malformed lines
            }
        }

        if (currentExchange.Messages.Count > 0)
            session.Exchanges.Add(currentExchange);

        // Derive slug from first user message
        var firstUserMsg = session.Exchanges
            .SelectMany(e => e.Messages)
            .FirstOrDefault(m => m.Role == "user");

        if (firstUserMsg != null)
        {
            session.Slug = GenerateSlug(firstUserMsg.TextContent);
        }

        return session;
    }

    private Message? ParseJsonlMessage(JsonElement root)
    {
        var message = new Message
        {
            Id = GetStringProp(root, "uuid") ?? Guid.NewGuid().ToString()
        };

        if (root.TryGetProperty("type", out var typeProp))
        {
            var type = typeProp.GetString();
            message.Role = type switch
            {
                "human" => "user",
                "assistant" => "agent",
                _ => type ?? "unknown"
            };
        }
        else
        {
            return null;
        }

        message.Timestamp = GetStringProp(root, "timestamp");
        message.Model = GetStringProp(root, "model");

        // Parse content
        if (root.TryGetProperty("message", out var msgProp))
        {
            if (root.TryGetProperty("content", out var contentProp))
            {
                ParseContent(contentProp, message);
            }
            else if (msgProp.ValueKind == JsonValueKind.Object &&
                     msgProp.TryGetProperty("content", out var innerContent))
            {
                ParseContent(innerContent, message);
            }
        }
        else if (root.TryGetProperty("content", out var contentProp))
        {
            ParseContent(contentProp, message);
        }

        // Parse usage
        if (root.TryGetProperty("usage", out var usageProp))
        {
            message.Usage = new Usage
            {
                InputTokens = GetIntProp(usageProp, "input_tokens"),
                OutputTokens = GetIntProp(usageProp, "output_tokens"),
                CacheCreationInputTokens = GetNullableIntProp(usageProp, "cache_creation_input_tokens"),
                CacheReadInputTokens = GetNullableIntProp(usageProp, "cache_read_input_tokens")
            };
        }

        return message;
    }

    private void ParseContent(JsonElement contentProp, Message message)
    {
        if (contentProp.ValueKind == JsonValueKind.String)
        {
            message.Content.Add(new ContentPart
            {
                Type = "text",
                Text = contentProp.GetString() ?? ""
            });
        }
        else if (contentProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in contentProp.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    message.Content.Add(new ContentPart
                    {
                        Type = "text",
                        Text = item.GetString() ?? ""
                    });
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    var type = GetStringProp(item, "type") ?? "text";

                    if (type == "tool_use")
                    {
                        message.Tool = new ToolInfo
                        {
                            Name = GetStringProp(item, "name") ?? "unknown",
                            UseId = GetStringProp(item, "id"),
                            Type = ClassifyToolType(GetStringProp(item, "name") ?? "")
                        };

                        if (item.TryGetProperty("input", out var inputProp))
                        {
                            message.Tool.Input = JsonSerializer
                                .Deserialize<Dictionary<string, JsonElement>>(inputProp.GetRawText());
                        }
                    }
                    else if (type == "tool_result")
                    {
                        // Tool results are part of agent responses
                        var text = GetStringProp(item, "content") ?? GetStringProp(item, "output") ?? "";
                        if (!string.IsNullOrEmpty(text))
                        {
                            message.Content.Add(new ContentPart { Type = "text", Text = text });
                        }
                    }
                    else if (type == "thinking")
                    {
                        message.Content.Add(new ContentPart
                        {
                            Type = "thinking",
                            Text = GetStringProp(item, "thinking") ?? GetStringProp(item, "text") ?? ""
                        });
                    }
                    else
                    {
                        message.Content.Add(new ContentPart
                        {
                            Type = type,
                            Text = GetStringProp(item, "text") ?? ""
                        });
                    }
                }
            }
        }
    }

    private static string ClassifyToolType(string name)
    {
        return name.ToLowerInvariant() switch
        {
            var n when n.Contains("read") || n.Contains("cat") || n.Contains("view") => "read",
            var n when n.Contains("write") || n.Contains("edit") || n.Contains("create") => "write",
            var n when n.Contains("search") || n.Contains("grep") || n.Contains("find") ||
                       n.Contains("glob") => "search",
            var n when n.Contains("bash") || n.Contains("shell") || n.Contains("exec") ||
                       n.Contains("command") => "shell",
            var n when n.Contains("task") || n.Contains("todo") => "task",
            _ => "generic"
        };
    }

    private static string? FindExecutable(string name)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                var fullPath = Path.Combine(p, name);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }
        catch { }
        return null;
    }

    private static string? GetVersion(string cmd)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(cmd, "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool HasSessionFiles(string dir)
    {
        return Directory.Exists(dir) && Directory.GetFiles(dir, "*.jsonl").Length > 0;
    }

    private static string? FindSessionFile(string projectDir, string sessionId)
    {
        var path = Path.Combine(projectDir, sessionId + ".jsonl");
        return File.Exists(path) ? path : null;
    }

    private static string DecodeProjectPath(string encodedName)
    {
        // Claude Code encodes project paths in directory names using URL-like encoding
        return Uri.UnescapeDataString(encodedName.Replace("+", " "));
    }

    private static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "untitled";

        var slug = text.ToLowerInvariant()
            .Replace("\n", " ")
            .Replace("\r", "");

        // Keep only alphanumeric and spaces, then convert spaces to hyphens
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        slug = string.Join("-", slug.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return slug.Length > 80 ? slug[..80] : slug;
    }

    private static string? GetStringProp(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static int GetIntProp(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : 0;
    }

    private static int? GetNullableIntProp(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : null;
    }
}
