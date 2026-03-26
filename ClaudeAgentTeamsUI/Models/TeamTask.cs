namespace ClaudeAgentTeamsUI.Models;

public class TeamTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TeamId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskState Status { get; set; } = TaskState.Pending;
    public ReviewState ReviewStatus { get; set; } = ReviewState.None;
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? CreatedBy { get; set; }
    public int Priority { get; set; }
    public int KanbanOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<TaskHistoryEvent> History { get; set; } = new();
    public List<TaskAttachment> Attachments { get; set; } = new();
    public List<WorkInterval> WorkIntervals { get; set; } = new();
    public string? BlockedBy { get; set; }
    public List<string> Tags { get; set; } = new();
}

public enum TaskState
{
    Pending,
    InProgress,
    InReview,
    Completed,
    Deleted
}

public enum ReviewState
{
    None,
    Review,
    NeedsFix,
    Approved
}

public class TaskHistoryEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class TaskAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? AddedBy { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class WorkInterval
{
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? MemberId { get; set; }
    public int TokensUsed { get; set; }
}

public class KanbanColumn
{
    public string Name { get; set; } = string.Empty;
    public TaskState State { get; set; }
    public List<TeamTask> Tasks { get; set; } = new();
    public int Count => Tasks.Count;
}
