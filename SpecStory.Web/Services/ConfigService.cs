using SpecStory.Web.Models;
using Tomlyn;
using Tomlyn.Model;

namespace SpecStory.Web.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    public string GetUserConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".specstory", "cli", "config.toml");
    }

    public string GetProjectConfigPath(string? projectPath = null)
    {
        var path = projectPath ?? Directory.GetCurrentDirectory();
        return Path.Combine(path, ".specstory", "cli", "config.toml");
    }

    public SpecStoryConfig GetUserConfig()
    {
        return LoadConfig(GetUserConfigPath());
    }

    public SpecStoryConfig? GetProjectConfig(string? projectPath = null)
    {
        var path = GetProjectConfigPath(projectPath);
        if (!File.Exists(path))
            return null;
        return LoadConfig(path);
    }

    public SpecStoryConfig GetEffectiveConfig(string? projectPath = null)
    {
        var userConfig = GetUserConfig();
        var projectConfig = GetProjectConfig(projectPath);

        if (projectConfig == null)
            return userConfig;

        // Merge: project config overrides user config
        return MergeConfigs(userConfig, projectConfig);
    }

    public void SaveUserConfig(SpecStoryConfig config)
    {
        var path = GetUserConfigPath();
        SaveConfig(path, config);
    }

    public void SaveProjectConfig(SpecStoryConfig config, string? projectPath = null)
    {
        var path = GetProjectConfigPath(projectPath);
        SaveConfig(path, config);
    }

    private SpecStoryConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
            return new SpecStoryConfig();

        try
        {
            var toml = File.ReadAllText(path);
            var table = Toml.ToModel(toml);
            return MapToConfig(table);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load config from {Path}", path);
            return new SpecStoryConfig();
        }
    }

    private void SaveConfig(string path, SpecStoryConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var toml = GenerateToml(config);
        File.WriteAllText(path, toml);
    }

    private static SpecStoryConfig MapToConfig(TomlTable table)
    {
        var config = new SpecStoryConfig();

        if (table.TryGetValue("local_sync", out var localSync) && localSync is TomlTable ls)
        {
            if (ls.TryGetValue("enabled", out var v)) config.LocalSync.Enabled = (bool)v;
            if (ls.TryGetValue("output_dir", out v)) config.LocalSync.OutputDir = (string)v;
            if (ls.TryGetValue("local_time_zone", out v)) config.LocalSync.LocalTimeZone = (bool)v;
        }

        if (table.TryGetValue("cloud_sync", out var cloudSync) && cloudSync is TomlTable cs)
        {
            if (cs.TryGetValue("enabled", out var v)) config.CloudSync.Enabled = (bool)v;
            if (cs.TryGetValue("cloud_url", out v)) config.CloudSync.CloudUrl = (string)v;
        }

        if (table.TryGetValue("logging", out var logging) && logging is TomlTable log)
        {
            if (log.TryGetValue("console", out var v)) config.Logging.Console = (bool)v;
            if (log.TryGetValue("log", out v)) config.Logging.Log = (bool)v;
            if (log.TryGetValue("debug", out v)) config.Logging.Debug = (bool)v;
            if (log.TryGetValue("debug_dir", out v)) config.Logging.DebugDir = (string)v;
            if (log.TryGetValue("silent", out v)) config.Logging.Silent = (bool)v;
        }

        if (table.TryGetValue("analytics", out var analytics) && analytics is TomlTable an)
        {
            if (an.TryGetValue("enabled", out var v)) config.Analytics.Enabled = (bool)v;
        }

        if (table.TryGetValue("telemetry", out var telemetry) && telemetry is TomlTable tel)
        {
            if (tel.TryGetValue("endpoint", out var v)) config.Telemetry.Endpoint = (string)v;
            if (tel.TryGetValue("service_name", out v)) config.Telemetry.ServiceName = (string)v;
            if (tel.TryGetValue("prompts", out v)) config.Telemetry.Prompts = (bool)v;
        }

        if (table.TryGetValue("providers", out var providers) && providers is TomlTable prov)
        {
            if (prov.TryGetValue("claude_cmd", out var v)) config.Providers.ClaudeCmd = (string)v;
            if (prov.TryGetValue("codex_cmd", out v)) config.Providers.CodexCmd = (string)v;
            if (prov.TryGetValue("cursor_cmd", out v)) config.Providers.CursorCmd = (string)v;
            if (prov.TryGetValue("droid_cmd", out v)) config.Providers.DroidCmd = (string)v;
            if (prov.TryGetValue("gemini_cmd", out v)) config.Providers.GeminiCmd = (string)v;
        }

        return config;
    }

    private static string GenerateToml(SpecStoryConfig config)
    {
        return $@"[local_sync]
enabled = {config.LocalSync.Enabled.ToString().ToLower()}
output_dir = ""{config.LocalSync.OutputDir}""
local_time_zone = {config.LocalSync.LocalTimeZone.ToString().ToLower()}

[cloud_sync]
enabled = {config.CloudSync.Enabled.ToString().ToLower()}
cloud_url = ""{config.CloudSync.CloudUrl}""

[logging]
console = {config.Logging.Console.ToString().ToLower()}
log = {config.Logging.Log.ToString().ToLower()}
debug = {config.Logging.Debug.ToString().ToLower()}
debug_dir = ""{config.Logging.DebugDir}""
silent = {config.Logging.Silent.ToString().ToLower()}

[version_check]
enabled = {config.VersionCheck.Enabled.ToString().ToLower()}

[analytics]
enabled = {config.Analytics.Enabled.ToString().ToLower()}

[telemetry]
endpoint = ""{config.Telemetry.Endpoint}""
service_name = ""{config.Telemetry.ServiceName}""
prompts = {config.Telemetry.Prompts.ToString().ToLower()}

[providers]
claude_cmd = ""{config.Providers.ClaudeCmd}""
codex_cmd = ""{config.Providers.CodexCmd}""
cursor_cmd = ""{config.Providers.CursorCmd}""
droid_cmd = ""{config.Providers.DroidCmd}""
gemini_cmd = ""{config.Providers.GeminiCmd}""
";
    }

    private static SpecStoryConfig MergeConfigs(SpecStoryConfig baseConfig, SpecStoryConfig overrideConfig)
    {
        // Simple merge: override takes precedence for non-default values
        return new SpecStoryConfig
        {
            LocalSync = new LocalSyncConfig
            {
                Enabled = overrideConfig.LocalSync.Enabled,
                OutputDir = overrideConfig.LocalSync.OutputDir,
                LocalTimeZone = overrideConfig.LocalSync.LocalTimeZone || baseConfig.LocalSync.LocalTimeZone
            },
            CloudSync = new CloudSyncConfig
            {
                Enabled = overrideConfig.CloudSync.Enabled,
                CloudUrl = !string.IsNullOrEmpty(overrideConfig.CloudSync.CloudUrl) &&
                           overrideConfig.CloudSync.CloudUrl != "https://cloud.specstory.com"
                    ? overrideConfig.CloudSync.CloudUrl
                    : baseConfig.CloudSync.CloudUrl
            },
            Logging = overrideConfig.Logging,
            VersionCheck = overrideConfig.VersionCheck,
            Analytics = overrideConfig.Analytics,
            Telemetry = overrideConfig.Telemetry,
            Providers = overrideConfig.Providers
        };
    }
}
