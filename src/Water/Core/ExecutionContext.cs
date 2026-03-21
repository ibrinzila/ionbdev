namespace Water.Core;

/// <summary>
/// Execution context passed to every task containing metadata and execution state.
/// </summary>
public class ExecutionContext
{
    public string FlowId { get; set; }
    public string ExecutionId { get; set; }
    public string? TaskId { get; set; }
    public int StepNumber { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public Dictionary<string, object?> FlowMetadata { get; set; }
    public Dictionary<string, object?> InitialInput { get; set; }

    public DateTime ExecutionStartTime { get; set; } = DateTime.UtcNow;
    public DateTime StepStartTime { get; set; } = DateTime.UtcNow;

    private readonly Dictionary<string, Dictionary<string, object?>> _taskOutputs = new();
    private readonly List<Dictionary<string, object?>> _stepHistory = new();
    private readonly Dictionary<string, object> _services = new();

    public ExecutionContext(
        string flowId,
        string? executionId = null,
        string? taskId = null,
        int stepNumber = 0,
        int attemptNumber = 1,
        Dictionary<string, object?>? flowMetadata = null,
        Dictionary<string, object?>? inputData = null)
    {
        FlowId = flowId;
        ExecutionId = executionId ?? $"exec_{Guid.NewGuid().ToString("N")[..8]}";
        TaskId = taskId;
        StepNumber = stepNumber;
        AttemptNumber = attemptNumber;
        FlowMetadata = flowMetadata ?? new();
        InitialInput = inputData ?? new();
    }

    public void RegisterService(string name, object service)
    {
        _services[name] = service;
    }

    public T GetService<T>(string name) where T : class
    {
        if (!_services.TryGetValue(name, out var service))
            throw new KeyNotFoundException($"Service '{name}' not found. Available services: {string.Join(", ", _services.Keys)}");

        if (service is not T typed)
            throw new InvalidCastException($"Service '{name}' is of type {service.GetType().Name}, expected {typeof(T).Name}");

        return typed;
    }

    public object GetService(string name)
    {
        if (!_services.TryGetValue(name, out var service))
            throw new KeyNotFoundException($"Service '{name}' not found. Available services: {string.Join(", ", _services.Keys)}");
        return service;
    }

    public bool HasService(string name) => _services.ContainsKey(name);

    public void AddTaskOutput(string taskId, Dictionary<string, object?> output)
    {
        _taskOutputs[taskId] = output;
        _stepHistory.Add(new Dictionary<string, object?>
        {
            ["step_number"] = StepNumber,
            ["task_id"] = taskId,
            ["output"] = output,
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["attempt_number"] = AttemptNumber
        });
    }

    public Dictionary<string, object?>? GetTaskOutput(string taskId)
    {
        return _taskOutputs.TryGetValue(taskId, out var output) ? output : null;
    }

    public Dictionary<string, Dictionary<string, object?>> GetAllTaskOutputs()
    {
        return new Dictionary<string, Dictionary<string, object?>>(_taskOutputs);
    }

    public List<Dictionary<string, object?>> GetStepHistory()
    {
        return new List<Dictionary<string, object?>>(_stepHistory);
    }

    public ExecutionContext CreateChildContext(string taskId, int? stepNumber = null, int attemptNumber = 1)
    {
        var child = new ExecutionContext(
            FlowId,
            ExecutionId,
            taskId,
            stepNumber ?? StepNumber + 1,
            attemptNumber,
            new Dictionary<string, object?>(FlowMetadata),
            new Dictionary<string, object?>(InitialInput));

        // Deep-copy mutable state
        foreach (var kvp in _taskOutputs)
            child._taskOutputs[kvp.Key] = new Dictionary<string, object?>(kvp.Value);
        foreach (var item in _stepHistory)
            child._stepHistory.Add(new Dictionary<string, object?>(item));
        foreach (var kvp in _services)
            child._services[kvp.Key] = kvp.Value;

        child.ExecutionStartTime = ExecutionStartTime;
        return child;
    }

    public Dictionary<string, object?> ToDict()
    {
        return new Dictionary<string, object?>
        {
            ["flow_id"] = FlowId,
            ["execution_id"] = ExecutionId,
            ["task_id"] = TaskId,
            ["step_number"] = StepNumber,
            ["attempt_number"] = AttemptNumber,
            ["flow_metadata"] = FlowMetadata,
            ["initial_input"] = InitialInput,
            ["execution_start_time"] = ExecutionStartTime.ToString("O"),
            ["step_start_time"] = StepStartTime.ToString("O"),
            ["task_outputs"] = _taskOutputs,
            ["step_history"] = _stepHistory
        };
    }

    // Internal accessors for the engine
    internal Dictionary<string, Dictionary<string, object?>> TaskOutputsInternal => _taskOutputs;
    internal List<Dictionary<string, object?>> StepHistoryInternal => _stepHistory;
    internal Dictionary<string, object> ServicesInternal => _services;
}
