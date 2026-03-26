namespace ClaudeAgentTeamsUI.Models;

public class ChangeReview
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TeamId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public List<FileChange> Files { get; set; } = new();
    public ReviewDecisionState State { get; set; } = ReviewDecisionState.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}

public enum ReviewDecisionState
{
    Pending,
    Approved,
    Rejected,
    PartiallyApproved
}

public class FileChange
{
    public string FilePath { get; set; } = string.Empty;
    public string? OldContent { get; set; }
    public string? NewContent { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public bool IsNewFile { get; set; }
    public bool IsDeleted { get; set; }
    public List<DiffHunk> Hunks { get; set; } = new();
    public List<FileEditEvent> EditTimeline { get; set; } = new();
}

public class DiffHunk
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public HunkDecision Decision { get; set; } = HunkDecision.Pending;
}

public enum HunkDecision
{
    Pending,
    Accepted,
    Rejected
}

public class FileEditEvent
{
    public string ToolName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int LinesChanged { get; set; }
}

public class FileReviewDecision
{
    public string FilePath { get; set; } = string.Empty;
    public HunkDecision OverallDecision { get; set; } = HunkDecision.Pending;
    public Dictionary<int, HunkDecision> HunkDecisions { get; set; } = new();
}

public class ConflictCheckResult
{
    public bool HasConflicts { get; set; }
    public List<string> ConflictFiles { get; set; } = new();
}
