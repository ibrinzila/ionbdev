namespace Water.Storage;

public enum FlowStatus
{
    Pending,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}

/// <summary>
/// Represents a single execution of a flow.
/// </summary>
public class FlowSession
{
    public string ExecutionId { get; set; }
    public string FlowId { get; set; }
    public FlowStatus Status { get; set; }
    public Dictionary<string, object?> InputData { get; set; }
    public int CurrentNodeIndex { get; set; }
    public Dictionary<string, object?> CurrentData { get; set; }
    public Dictionary<string, object?> ContextState { get; set; } = new();
    public Dictionary<string, object?>? Result { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public FlowSession(
        string flowId,
        Dictionary<string, object?> inputData,
        string? executionId = null,
        FlowStatus status = FlowStatus.Pending)
    {
        ExecutionId = executionId ?? $"exec_{Guid.NewGuid().ToString("N")[..8]}";
        FlowId = flowId;
        Status = status;
        InputData = inputData;
        CurrentData = new Dictionary<string, object?>(inputData);
    }
}

/// <summary>
/// Represents a single execution of a task within a flow session.
/// </summary>
public class TaskRun
{
    public string Id { get; set; }
    public string ExecutionId { get; set; }
    public string TaskId { get; set; }
    public int NodeIndex { get; set; }
    public FlowStatus Status { get; set; }
    public Dictionary<string, object?>? InputData { get; set; }
    public Dictionary<string, object?>? OutputData { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TaskRun(
        string executionId,
        string taskId,
        int nodeIndex,
        FlowStatus status = FlowStatus.Pending,
        Dictionary<string, object?>? inputData = null,
        Dictionary<string, object?>? outputData = null,
        string? error = null,
        DateTime? startedAt = null,
        DateTime? completedAt = null,
        string? id = null)
    {
        Id = id ?? $"run_{Guid.NewGuid().ToString("N")[..8]}";
        ExecutionId = executionId;
        TaskId = taskId;
        NodeIndex = nodeIndex;
        Status = status;
        InputData = inputData;
        OutputData = outputData;
        Error = error;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }
}

/// <summary>
/// Abstract base class for flow execution storage backends.
/// </summary>
public interface IStorageBackend
{
    Task SaveSessionAsync(FlowSession session);
    Task<FlowSession?> GetSessionAsync(string executionId);
    Task<List<FlowSession>> ListSessionsAsync(string? flowId = null);
    Task SaveTaskRunAsync(TaskRun taskRun);
    Task<List<TaskRun>> GetTaskRunsAsync(string executionId);
}
