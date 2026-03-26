using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgentTeamsUI.Data;

public class JsonDataStore
{
    private readonly string _dataDir;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    public JsonDataStore(string? dataDir = null)
    {
        _dataDir = dataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-teams-ui", "data");
        Directory.CreateDirectory(_dataDir);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<T> LoadAsync<T>(string fileName) where T : new()
    {
        var path = Path.Combine(_dataDir, fileName);
        var lockObj = _fileLocks.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();
        try
        {
            if (!File.Exists(path)) return new T();
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task SaveAsync<T>(string fileName, T data)
    {
        var path = Path.Combine(_dataDir, fileName);
        var lockObj = _fileLocks.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public string DataDirectory => _dataDir;
}
