using System.Text.Json;
using System.Text.Json.Serialization;
using Codapter.Core.Models;

namespace Codapter.Core.Services;

/// <summary>
/// In-memory config store with TOML persistence.
/// Port of packages/core/src/config-store.ts
/// </summary>
public class InMemoryConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly string _filePath;
    private Dictionary<string, object?> _data;
    private long _version;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public InMemoryConfigStore(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultPath();
        _data = GetDefaults();
        _version = 0;
    }

    private static string GetDefaultPath()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(configDir, "codapter", "config.json");
    }

    private static Dictionary<string, object?> GetDefaults() => new()
    {
        ["model"] = null,
        ["review_model"] = null,
        ["sandbox_mode"] = "full_access",
        ["web_search"] = false,
        ["tools"] = new Dictionary<string, object>(),
        ["instructions"] = null,
        ["reasoning_effort"] = "medium",
        ["reasoning_summary"] = null
    };

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(_filePath))
            {
                var content = await File.ReadAllTextAsync(_filePath, ct);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                if (parsed is not null)
                {
                    foreach (var (key, value) in parsed)
                    {
                        _data[key] = ConvertJsonElement(value);
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback to defaults
        }
        finally
        {
            _lock.Release();
        }
    }

    public ConfigReadResponse Read()
    {
        return new ConfigReadResponse
        {
            Config = ToConfig(),
            Version = _version,
            Layers = new List<ConfigLayer>
            {
                new()
                {
                    Source = ConfigLayerSource.User,
                    Config = ToConfig()
                }
            }
        };
    }

    public ConfigWriteResponse WriteValue(long expectedVersion, ConfigEdit edit)
    {
        AssertVersion(expectedVersion);
        ApplyEdit(edit);
        _version++;
        PersistSync();

        return new ConfigWriteResponse
        {
            Version = _version,
            Config = ToConfig()
        };
    }

    public ConfigWriteResponse WriteBatch(long expectedVersion, List<ConfigEdit> edits)
    {
        AssertVersion(expectedVersion);

        foreach (var edit in edits)
        {
            ApplyEdit(edit);
        }

        _version++;
        PersistSync();

        return new ConfigWriteResponse
        {
            Version = _version,
            Config = ToConfig()
        };
    }

    private void ApplyEdit(ConfigEdit edit)
    {
        var keys = edit.Key.Split('.');
        var container = EnsureContainer(_data, keys[..^1]);
        var finalKey = keys[^1];

        if (edit.Strategy == "upsert" &&
            container.TryGetValue(finalKey, out var existing) &&
            existing is Dictionary<string, object?> existingDict &&
            edit.Value.ValueKind == JsonValueKind.Object)
        {
            // Recursive merge for upsert
            MergeInto(existingDict, edit.Value);
        }
        else
        {
            container[finalKey] = ConvertJsonElement(edit.Value);
        }
    }

    private static Dictionary<string, object?> EnsureContainer(
        Dictionary<string, object?> root, string[] path)
    {
        var current = root;
        foreach (var key in path)
        {
            if (!current.TryGetValue(key, out var value) || value is not Dictionary<string, object?> dict)
            {
                dict = new Dictionary<string, object?>();
                current[key] = dict;
            }
            current = dict;
        }
        return current;
    }

    private static void MergeInto(Dictionary<string, object?> target, JsonElement source)
    {
        foreach (var prop in source.EnumerateObject())
        {
            target[prop.Name] = ConvertJsonElement(prop.Value);
        }
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
        _ => null
    };

    private void AssertVersion(long expected)
    {
        if (expected != _version)
            throw new InvalidOperationException(
                $"Config version mismatch: expected {expected}, current is {_version}");
    }

    private Config ToConfig() => new()
    {
        Model = _data.GetValueOrDefault("model") as string,
        ReviewModel = _data.GetValueOrDefault("review_model") as string,
        SandboxModeValue = ParseEnum<SandboxMode>(_data.GetValueOrDefault("sandbox_mode") as string),
        WebSearch = _data.GetValueOrDefault("web_search") as bool?,
        Instructions = _data.GetValueOrDefault("instructions") as string,
        ReasoningEffortValue = ParseEnum<ReasoningEffort>(_data.GetValueOrDefault("reasoning_effort") as string),
        ReasoningSummary = _data.GetValueOrDefault("reasoning_summary") as string
    };

    private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
        value is not null && Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : null;

    private void PersistSync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_data, JsonOptions);
            var tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _filePath, overwrite: true);
        }
        catch
        {
            // Best-effort persistence
        }
    }
}
