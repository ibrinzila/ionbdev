namespace Water.Core;

/// <summary>
/// Represents a failed task execution captured in the dead letter queue.
/// </summary>
public class DeadLetter
{
    public string TaskId { get; set; }
    public Dictionary<string, object?> InputData { get; set; }
    public string Error { get; set; }
    public string? ExecutionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public DeadLetter(string taskId, Dictionary<string, object?> inputData, string error, string? executionId = null)
    {
        TaskId = taskId;
        InputData = inputData;
        Error = error;
        ExecutionId = executionId;
    }
}

/// <summary>
/// Interface for dead letter queues that capture failed task executions.
/// </summary>
public interface IDeadLetterQueue
{
    Task EnqueueAsync(DeadLetter letter);
    Task<List<DeadLetter>> GetAllAsync();
    Task<int> CountAsync();
    Task ClearAsync();
}

/// <summary>
/// In-memory dead letter queue implementation.
/// </summary>
public class InMemoryDlq : IDeadLetterQueue
{
    private readonly List<DeadLetter> _letters = new();
    private readonly object _lock = new();

    public Task EnqueueAsync(DeadLetter letter)
    {
        lock (_lock) { _letters.Add(letter); }
        return Task.CompletedTask;
    }

    public Task<List<DeadLetter>> GetAllAsync()
    {
        lock (_lock) { return Task.FromResult(new List<DeadLetter>(_letters)); }
    }

    public Task<int> CountAsync()
    {
        lock (_lock) { return Task.FromResult(_letters.Count); }
    }

    public Task ClearAsync()
    {
        lock (_lock) { _letters.Clear(); }
        return Task.CompletedTask;
    }
}
