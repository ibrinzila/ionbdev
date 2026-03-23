using System.Text.Json;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services.Providers;

/// <summary>
/// Provider for Cursor CLI sessions. Reads SQLite databases from ~/.cursor/chats/.
/// Requires Microsoft.Data.Sqlite NuGet package.
/// </summary>
public class CursorCliProvider : IAgentProvider
{
    public string Id => "cursor-cli";
    public string Name => "Cursor CLI";

    private string GetChatsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cursor", "chats");
    }

    public ProviderCheckResult Check(string? customCommand = null)
    {
        var cmd = customCommand ?? "cursor-agent";
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
        var dir = GetChatsDir();
        return Directory.Exists(dir) &&
               (Directory.GetFiles(dir, "*.db").Length > 0 ||
                Directory.GetFiles(dir, "*.sqlite").Length > 0);
    }

    public async Task<SessionData?> GetSessionAsync(string projectPath, string sessionId,
        bool debugRaw = false)
    {
        var dir = GetChatsDir();
        if (!Directory.Exists(dir))
            return null;

        foreach (var dbFile in GetDatabaseFiles(dir))
        {
            try
            {
                var sessions = await ReadSqliteSessionsAsync(dbFile);
                var match = sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (match != null)
                    return match;
            }
            catch { }
        }

        return null;
    }

    public async Task<List<SessionData>> GetSessionsAsync(string projectPath, bool debugRaw = false,
        IProgress<int>? progress = null)
    {
        var sessions = new List<SessionData>();
        var dir = GetChatsDir();
        if (!Directory.Exists(dir))
            return sessions;

        foreach (var dbFile in GetDatabaseFiles(dir))
        {
            try
            {
                var dbSessions = await ReadSqliteSessionsAsync(dbFile);
                sessions.AddRange(dbSessions);
                progress?.Report(sessions.Count);
            }
            catch { }
        }

        return sessions;
    }

    public Task<List<SessionMetadata>> ListSessionsAsync(string projectPath)
    {
        var metadata = new List<SessionMetadata>();
        var dir = GetChatsDir();
        if (!Directory.Exists(dir))
            return Task.FromResult(metadata);

        foreach (var dbFile in GetDatabaseFiles(dir))
        {
            try
            {
                var info = new FileInfo(dbFile);
                metadata.Add(new SessionMetadata
                {
                    SessionId = Path.GetFileNameWithoutExtension(dbFile),
                    ProviderId = Id,
                    ProviderName = Name,
                    CreatedAt = info.CreationTimeUtc.ToString("o"),
                    FilePath = dbFile
                });
            }
            catch { }
        }

        return Task.FromResult(metadata);
    }

    private async Task<List<SessionData>> ReadSqliteSessionsAsync(string dbPath)
    {
        var sessions = new List<SessionData>();

        // Use Microsoft.Data.Sqlite to read chat data
        var connectionString = $"Data Source={dbPath};Mode=ReadOnly";
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Read messages ordered by rowid
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT rowid, * FROM messages
            ORDER BY rowid ASC";

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var currentSession = new SessionData
            {
                SchemaVersion = "1",
                Provider = new ProviderInfo { Id = Id, Name = Name },
                SessionId = Path.GetFileNameWithoutExtension(dbPath)
            };

            var currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };

            while (await reader.ReadAsync())
            {
                var role = reader["role"]?.ToString() ?? "unknown";
                var content = reader["content"]?.ToString() ?? "";

                var message = new Message
                {
                    Id = reader["rowid"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Role = role == "user" ? "user" : "agent",
                    Content = new List<ContentPart>
                    {
                        new() { Type = "text", Text = content }
                    }
                };

                if (message.Role == "user" && currentExchange.Messages.Count > 0)
                {
                    currentSession.Exchanges.Add(currentExchange);
                    currentExchange = new Exchange { ExchangeId = Guid.NewGuid().ToString() };
                }

                currentExchange.Messages.Add(message);
            }

            if (currentExchange.Messages.Count > 0)
                currentSession.Exchanges.Add(currentExchange);

            if (currentSession.Exchanges.Count > 0)
                sessions.Add(currentSession);
        }
        catch
        {
            // Table might not exist or have different schema
        }

        return sessions;
    }

    private static IEnumerable<string> GetDatabaseFiles(string dir)
    {
        var files = new List<string>();
        if (Directory.Exists(dir))
        {
            files.AddRange(Directory.GetFiles(dir, "*.db"));
            files.AddRange(Directory.GetFiles(dir, "*.sqlite"));
        }
        return files;
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
}
