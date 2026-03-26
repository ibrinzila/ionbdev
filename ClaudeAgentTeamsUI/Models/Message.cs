namespace ClaudeAgentTeamsUI.Models;

public class InboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TeamId { get; set; } = string.Empty;
    public string FromMember { get; set; } = string.Empty;
    public string ToMember { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageSource Source { get; set; } = MessageSource.Direct;
    public string? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public bool IsRead { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<TaskAttachment> Attachments { get; set; } = new();
    public string? ToolUsageSummary { get; set; }
}

public enum MessageSource
{
    Direct,
    TaskComment,
    CrossTeam,
    CrossTeamSent,
    System
}

public class CrossTeamMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromTeam { get; set; } = string.Empty;
    public string ToTeam { get; set; } = string.Empty;
    public string FromMember { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
