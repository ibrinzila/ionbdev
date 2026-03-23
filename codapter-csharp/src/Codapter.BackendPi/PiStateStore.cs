using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codapter.BackendPi;

/// <summary>
/// Persistent state store for Pi backend sessions.
/// Port of packages/backend-pi/src/state-store.ts
/// </summary>

public record PiBackendSessionRecord
{
    [JsonPropertyName("opaqueSessionId")]
    public string OpaqueSessionId { get; init; } = default!;

    [JsonPropertyName("sessionFile")]
    public string SessionFile { get; init; } = default!;

    [JsonPropertyName("sessionName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionName { get; init; }

    [JsonPropertyName("modelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelId { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

internal record PiStateFile
{
    [JsonPropertyName("sessions")]
    public Dictionary<string, PiBackendSessionRecord> Sessions { get; init; } = new();
}

public class PiBackendStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private PiStateFile _state = new();

    public PiBackendStateStore(string directory)
    {
        _filePath = Path.Combine(directory, ".codapter-pi-backend.json");
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            var parsed = JsonSerializer.Deserialize<PiStateFile>(json, JsonOptions);
            if (parsed is not null)
                _state = parsed;
        }
        catch
        {
            // Graceful fallback
            _state = new PiStateFile();
        }
    }

    public PiBackendSessionRecord? Get(string sessionId)
    {
        return _state.Sessions.GetValueOrDefault(sessionId);
    }

    public void Upsert(PiBackendSessionRecord record)
    {
        _state.Sessions[record.OpaqueSessionId] = record;
        PersistSync();
    }

    public void Update(string sessionId, Action<PiBackendSessionRecord> updater)
    {
        if (_state.Sessions.TryGetValue(sessionId, out var record))
        {
            updater(record);
            _state.Sessions[sessionId] = record with { UpdatedAt = DateTimeOffset.UtcNow };
            PersistSync();
        }
    }

    public List<PiBackendSessionRecord> List()
    {
        return _state.Sessions.Values.ToList();
    }

    private void PersistSync()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_state, JsonOptions);
        var tmpPath = _filePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}
