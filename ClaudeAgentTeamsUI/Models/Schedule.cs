namespace ClaudeAgentTeamsUI.Models;

public class Schedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TeamName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;
    public int WarmUpMinutes { get; set; }
    public int? MaxTurns { get; set; }
    public decimal? MaxBudgetUsd { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int MaxConsecutiveFailures { get; set; } = 3;
    public ScheduleLaunchConfig LaunchConfig { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public List<ScheduleRun> Runs { get; set; } = new();
}

public enum ScheduleStatus
{
    Active,
    Paused,
    Disabled
}

public class ScheduleLaunchConfig
{
    public string? WorkingDirectory { get; set; }
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public string? Effort { get; set; }
    public List<string> AllowedTools { get; set; } = new();
}

public class ScheduleRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScheduleId { get; set; } = string.Empty;
    public ScheduleRunStatus Status { get; set; } = ScheduleRunStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? OutputSummary { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
}

public enum ScheduleRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
