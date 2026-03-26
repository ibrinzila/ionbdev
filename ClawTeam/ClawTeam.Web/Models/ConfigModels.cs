using System.Text.Json.Serialization;

namespace ClawTeam.Web.Models;

/// <summary>Runtime settings for an individual agent.</summary>
public class AgentProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("api_base")]
    public string? ApiBase { get; set; }

    [JsonPropertyName("command")]
    public List<string>? Command { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }
}

/// <summary>Shared configuration template with client-specific overrides.</summary>
public class AgentPreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("overrides")]
    public Dictionary<string, object>? Overrides { get; set; }
}

/// <summary>Master ClawTeam configuration.</summary>
public class ClawTeamConfig
{
    [JsonPropertyName("data_dir")]
    public string DataDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawteam");

    [JsonPropertyName("user")]
    public string User { get; set; } = Environment.UserName;

    [JsonPropertyName("default_team")]
    public string? DefaultTeam { get; set; }

    [JsonPropertyName("default_backend")]
    public string DefaultBackend { get; set; } = "subprocess";

    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "file";

    [JsonPropertyName("workspace")]
    public string Workspace { get; set; } = "auto";

    [JsonPropertyName("skip_permissions")]
    public bool SkipPermissions { get; set; } = true;

    [JsonPropertyName("spawn_timeout")]
    public int SpawnTimeout { get; set; } = 30;

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("profiles")]
    public Dictionary<string, AgentProfile> Profiles { get; set; } = new();

    [JsonPropertyName("presets")]
    public Dictionary<string, AgentPreset> Presets { get; set; } = new();
}
