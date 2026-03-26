namespace ClaudeAgentTeamsUI.Models;

public class DashboardViewModel
{
    public List<TeamSummary> Teams { get; set; } = new();
    public List<Project> RecentProjects { get; set; } = new();
    public int TotalSessions { get; set; }
    public int ActiveAgents { get; set; }
    public int PendingTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int UnreadNotifications { get; set; }
    public List<Notification> RecentNotifications { get; set; } = new();
    public SystemHealth Health { get; set; } = new();
}

public class SystemHealth
{
    public long MemoryUsageMb { get; set; }
    public double CpuPercent { get; set; }
    public TimeSpan Uptime { get; set; }
    public string DotNetVersion { get; set; } = string.Empty;
}

public class TeamDetailViewModel
{
    public Team Team { get; set; } = new();
    public List<KanbanColumn> KanbanColumns { get; set; } = new();
    public List<InboxMessage> RecentMessages { get; set; } = new();
    public int UnreadMessageCount { get; set; }
    public List<ToolApprovalRequest> PendingApprovals { get; set; } = new();
}

public class SessionDetailViewModel
{
    public Session Session { get; set; } = new();
    public List<WaterfallItem> Waterfall { get; set; } = new();
}

public class SessionListViewModel
{
    public List<SessionListItem> Sessions { get; set; } = new();
    public string? ProjectFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public bool HasMore => Page * PageSize < TotalCount;
}

public class MessagesViewModel
{
    public string TeamId { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public List<InboxMessage> Messages { get; set; } = new();
    public List<CrossTeamMessage> CrossTeamMessages { get; set; } = new();
    public int UnreadCount { get; set; }
}

public class ReviewViewModel
{
    public ChangeReview Review { get; set; } = new();
    public string TeamName { get; set; } = string.Empty;
    public string TaskTitle { get; set; } = string.Empty;
}

public class SettingsViewModel
{
    public AppConfig Config { get; set; } = new();
    public string? CliVersion { get; set; }
    public string? CliStatus { get; set; }
}

public class ScheduleListViewModel
{
    public List<Schedule> Schedules { get; set; } = new();
    public string? TeamFilter { get; set; }
}

public class KanbanBoardViewModel
{
    public string TeamId { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public List<KanbanColumn> Columns { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string? AssigneeFilter { get; set; }
    public string SortBy { get; set; } = "priority";
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
