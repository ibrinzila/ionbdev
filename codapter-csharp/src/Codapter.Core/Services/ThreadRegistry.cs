using System.Text.Json;
using System.Text.Json.Serialization;
using Codapter.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codapter.Core.Services;

/// <summary>
/// Persistent registry for thread metadata.
/// Port of packages/core/src/thread-registry.ts
/// </summary>

public record ThreadRegistryEntry
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("backendSessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BackendSessionId { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    [JsonPropertyName("git")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThreadGitInfo? Git { get; init; }

    [JsonPropertyName("status")]
    public ThreadStatus Status { get; init; } = ThreadStatus.Active;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("subAgentSpawn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubAgentSpawnInfo? SubAgentSpawn { get; init; }
}

public record SubAgentSpawnInfo
{
    [JsonPropertyName("parentThreadId")]
    public string ParentThreadId { get; init; } = default!;

    [JsonPropertyName("agentId")]
    public string AgentId { get; init; } = default!;

    [JsonPropertyName("nickname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nickname { get; init; }

    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("depth")]
    public int Depth { get; init; }
}

public record CreateThreadRegistryEntry
{
    public string? BackendSessionId { get; init; }
    public string? Title { get; init; }
    public string? Model { get; init; }
    public string? Path { get; init; }
    public ThreadGitInfo? Git { get; init; }
    public SubAgentSpawnInfo? SubAgentSpawn { get; init; }
}

public record UpdateThreadRegistryEntry
{
    public string? BackendSessionId { get; init; }
    public string? Title { get; init; }
    public string? Model { get; init; }
    public string? Path { get; init; }
    public ThreadGitInfo? Git { get; init; }
    public ThreadStatus? Status { get; init; }
}

public class ThreadRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly ILogger<ThreadRegistry>? _logger;
    private readonly Dictionary<string, ThreadRegistryEntry> _entries = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ThreadRegistry(string? filePath = null, ILogger<ThreadRegistry>? logger = null)
    {
        _filePath = filePath ?? GetDefaultPath();
        _logger = logger;
    }

    private static string GetDefaultPath()
    {
        var dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(dataDir, "codapter", "threads.json");
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger?.LogInformation("Thread registry file not found, starting fresh");
                return;
            }

            var json = await File.ReadAllTextAsync(_filePath, ct);
            var entries = JsonSerializer.Deserialize<Dictionary<string, ThreadRegistryEntry>>(json, JsonOptions);

            if (entries is not null)
            {
                _entries.Clear();
                foreach (var (key, entry) in entries)
                {
                    _entries[key] = entry;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse thread registry, starting fresh");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ThreadRegistryEntry>> ListAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _entries.Values
                .OrderByDescending(e => e.UpdatedAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThreadRegistryEntry?> GetAsync(string threadId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _entries.GetValueOrDefault(threadId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThreadRegistryEntry> CreateAsync(
        CreateThreadRegistryEntry input, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var entry = new ThreadRegistryEntry
            {
                ThreadId = Guid.NewGuid().ToString(),
                BackendSessionId = input.BackendSessionId,
                Title = input.Title,
                Model = input.Model,
                Path = input.Path,
                Git = input.Git,
                Status = ThreadStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                SubAgentSpawn = input.SubAgentSpawn
            };

            _entries[entry.ThreadId] = entry;
            await PersistInternalAsync(ct);
            return entry;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThreadRegistryEntry> UpdateAsync(
        string threadId, UpdateThreadRegistryEntry input, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_entries.TryGetValue(threadId, out var existing))
                throw new KeyNotFoundException($"Thread {threadId} not found");

            var updated = existing with
            {
                BackendSessionId = input.BackendSessionId ?? existing.BackendSessionId,
                Title = input.Title ?? existing.Title,
                Model = input.Model ?? existing.Model,
                Path = input.Path ?? existing.Path,
                Git = input.Git ?? existing.Git,
                Status = input.Status ?? existing.Status,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _entries[threadId] = updated;
            await PersistInternalAsync(ct);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string threadId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _entries.Remove(threadId);
            await PersistInternalAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PersistInternalAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(_entries, JsonOptions);
        await File.WriteAllTextAsync(tmpPath, json, ct);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}
