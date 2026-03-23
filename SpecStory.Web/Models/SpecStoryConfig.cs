namespace SpecStory.Web.Models;

public class SpecStoryConfig
{
    public LocalSyncConfig LocalSync { get; set; } = new();
    public CloudSyncConfig CloudSync { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public VersionCheckConfig VersionCheck { get; set; } = new();
    public AnalyticsConfig Analytics { get; set; } = new();
    public TelemetryConfig Telemetry { get; set; } = new();
    public ProvidersConfig Providers { get; set; } = new();
}

public class LocalSyncConfig
{
    public bool Enabled { get; set; } = true;
    public string OutputDir { get; set; } = ".specstory/history";
    public bool LocalTimeZone { get; set; }
}

public class CloudSyncConfig
{
    public bool Enabled { get; set; } = true;
    public string CloudUrl { get; set; } = "https://cloud.specstory.com";
}

public class LoggingConfig
{
    public bool Console { get; set; }
    public bool Log { get; set; }
    public bool Debug { get; set; }
    public string DebugDir { get; set; } = ".specstory/debug";
    public bool Silent { get; set; }
}

public class VersionCheckConfig
{
    public bool Enabled { get; set; } = true;
}

public class AnalyticsConfig
{
    public bool Enabled { get; set; } = true;
}

public class TelemetryConfig
{
    public string Endpoint { get; set; } = "localhost:4317";
    public string ServiceName { get; set; } = "specstory-web";
    public bool Prompts { get; set; } = true;
}

public class ProvidersConfig
{
    public string ClaudeCmd { get; set; } = "claude";
    public string CodexCmd { get; set; } = "codex";
    public string CursorCmd { get; set; } = "cursor-agent";
    public string DroidCmd { get; set; } = "droid";
    public string GeminiCmd { get; set; } = "gemini";
}
