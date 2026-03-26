using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// Manages ClawTeam configuration persistence with environment variable overrides.
/// Config stored at {DataDir}/config.json.
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Resolve the data directory from env, config, or default.</summary>
    public static string GetDataDir()
    {
        var envDir = Environment.GetEnvironmentVariable("CLAWTEAM_DATA_DIR");
        if (!string.IsNullOrEmpty(envDir)) return envDir;

        var defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawteam");
        return defaultDir;
    }

    private static string ConfigPath()
    {
        return Path.Combine(GetDataDir(), "config.json");
    }

    public ClawTeamConfig Load()
    {
        var path = ConfigPath();
        if (!File.Exists(path)) return new ClawTeamConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClawTeamConfig>(json, JsonOpts) ?? new ClawTeamConfig();
        }
        catch
        {
            return new ClawTeamConfig();
        }
    }

    public void Save(ClawTeamConfig config)
    {
        var path = ConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(config, JsonOpts);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Get effective value: env var > config file > default.
    /// </summary>
    public string GetEffective(string key, string defaultValue = "")
    {
        var envKey = $"CLAWTEAM_{key.ToUpperInvariant()}";
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(envVal)) return envVal;

        var config = Load();
        return key.ToLower() switch
        {
            "transport" => config.Transport,
            "workspace" => config.Workspace,
            "default_backend" => config.DefaultBackend,
            "user" => config.User,
            "data_dir" => config.DataDir,
            _ => defaultValue
        };
    }
}
