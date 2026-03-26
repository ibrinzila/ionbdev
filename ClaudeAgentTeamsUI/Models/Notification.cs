namespace ClaudeAgentTeamsUI.Models;

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public NotificationCategory Category { get; set; } = NotificationCategory.Info;
    public string? TeamEventType { get; set; }
    public string? TeamId { get; set; }
    public string? SessionId { get; set; }
    public string? SourceTool { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationCategory
{
    Info,
    Warning,
    Error,
    TeamEvent,
    TaskCompleted,
    RateLimit,
    ScheduleFailed
}

public class NotificationTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public TriggerMode Mode { get; set; } = TriggerMode.ErrorStatus;
    public string? Pattern { get; set; }
    public int? TokenThreshold { get; set; }
    public string? ToolFilter { get; set; }
    public string? RepositoryScope { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public enum TriggerMode
{
    ErrorStatus,
    ContentPattern,
    TokenThreshold
}
