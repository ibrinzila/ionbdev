namespace ClaudeAgentTeamsUI.Models;

public class AppConfig
{
    public NotificationConfig Notifications { get; set; } = new();
    public GeneralConfig General { get; set; } = new();
    public DisplayConfig Display { get; set; } = new();
    public HttpServerConfig HttpServer { get; set; } = new();
}

public class NotificationConfig
{
    public bool Enabled { get; set; } = true;
    public int SnoozeDurationMinutes { get; set; } = 30;
    public bool TeamEvents { get; set; } = true;
    public bool TaskCompletions { get; set; } = true;
    public bool Errors { get; set; } = true;
    public bool RateLimits { get; set; } = true;
    public List<NotificationTrigger> Triggers { get; set; } = new();
}

public class GeneralConfig
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "en";
    public bool TelemetryEnabled { get; set; }
    public string? ClaudeRootPath { get; set; }
}

public class DisplayConfig
{
    public bool ShowTimestamps { get; set; } = true;
    public bool CompactMode { get; set; }
    public bool SyntaxHighlighting { get; set; } = true;
}

public class HttpServerConfig
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 5199;
}
