namespace ClaudeAgentTeamsUI.Models;

public class Team
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public List<TeamMember> Members { get; set; } = new();
    public TeamStatus Status { get; set; } = TeamStatus.Idle;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public TeamProvisioningState? ProvisioningState { get; set; }
    public ToolApprovalSettings ApprovalSettings { get; set; } = new();
    public bool IsDeleted { get; set; }
}

public enum TeamStatus
{
    Idle,
    Launching,
    Running,
    Stopping,
    Stopped,
    Failed
}

public class TeamMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public MemberRole Role { get; set; } = MemberRole.Developer;
    public MemberStatus Status { get; set; } = MemberStatus.Idle;
    public string? SessionId { get; set; }
    public string? Color { get; set; }
    public string? WorkflowPrompt { get; set; }
    public DateTime? SpawnedAt { get; set; }
    public int TasksCompleted { get; set; }
    public int TokensUsed { get; set; }
}

public enum MemberRole
{
    Lead,
    Developer,
    Reviewer,
    Architect,
    Tester
}

public enum MemberStatus
{
    Active,
    Idle,
    Terminated,
    Unknown
}

public class TeamProvisioningState
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public ProvisioningStatus Status { get; set; } = ProvisioningStatus.Pending;
    public List<ProvisioningStep> Steps { get; set; } = new();
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum ProvisioningStatus
{
    Pending,
    Spawning,
    Configuring,
    Ready,
    Failed
}

public class ProvisioningStep
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Log { get; set; }
}

public class ToolApprovalSettings
{
    public bool AutoAllowFileEdits { get; set; }
    public bool AutoAllowSafeBash { get; set; }
    public bool AutoAllowRead { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string TimeoutAction { get; set; } = "wait";
}

public class ToolApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TeamName { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ToolInput { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Decision { get; set; }
}

public class TeamSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TeamStatus Status { get; set; }
    public int MemberCount { get; set; }
    public int ActiveMembers { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class TeamCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public List<TeamMemberInput> Members { get; set; } = new();
}

public class TeamMemberInput
{
    public string Name { get; set; } = string.Empty;
    public MemberRole Role { get; set; } = MemberRole.Developer;
    public string? WorkflowPrompt { get; set; }
}

public class TeamLaunchRequest
{
    public string? WorkingDirectory { get; set; }
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public bool UseWorktree { get; set; }
}
