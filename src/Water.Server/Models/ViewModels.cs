namespace Water.Server.Models;

public class HomeViewModel
{
    public int TotalFlows { get; set; }
    public int TotalSessions { get; set; }
    public int Running { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Paused { get; set; }
    public List<FlowCardViewModel> RecentFlows { get; set; } = new();
    public List<SessionRowViewModel> RecentSessions { get; set; } = new();
}

public class FlowCardViewModel
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public int TaskCount { get; set; }
    public List<TaskInfoDto> Tasks { get; set; } = new();
}

public class FlowDetailViewModel
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public List<TaskInfoDto> Tasks { get; set; } = new();
    public List<SessionRowViewModel> Sessions { get; set; } = new();
}

public class SessionRowViewModel
{
    public string ExecutionId { get; set; } = "";
    public string FlowId { get; set; } = "";
    public string Status { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string? Error { get; set; }
}

public class SessionDetailViewModel
{
    public string ExecutionId { get; set; } = "";
    public string FlowId { get; set; } = "";
    public string Status { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string? Error { get; set; }
    public string InputDataJson { get; set; } = "{}";
    public string CurrentDataJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public List<TaskRunViewModel> TaskRuns { get; set; } = new();
}

public class TaskRunViewModel
{
    public string Id { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string Status { get; set; } = "";
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? Error { get; set; }
}

public class DashboardViewModel
{
    public int TotalSessions { get; set; }
    public int Running { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Paused { get; set; }
    public double SuccessRate { get; set; }
    public List<SessionRowViewModel> Sessions { get; set; } = new();
}

public class RunFlowViewModel
{
    public string FlowId { get; set; } = "";
    public string Description { get; set; } = "";
}
