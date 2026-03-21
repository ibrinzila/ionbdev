namespace Water.Core;

/// <summary>
/// A single executable unit within a Water flow.
/// Tasks define an execute function that processes input data and returns output.
/// </summary>
public class WaterTask
{
    public string Id { get; set; }
    public string Description { get; set; }
    public Func<Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>> Execute { get; set; }
    public int RetryCount { get; set; }
    public double RetryDelay { get; set; }
    public double RetryBackoff { get; set; } = 1.0;
    public double? Timeout { get; set; }
    public bool ValidateSchema { get; set; }
    public double? RateLimit { get; set; }
    public ITaskCache? Cache { get; set; }
    public CircuitBreaker? CircuitBreaker { get; set; }

    // Schema types (optional, for documentation/validation)
    public Type? InputSchemaType { get; set; }
    public Type? OutputSchemaType { get; set; }

    public WaterTask(
        Func<Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>> execute,
        string? id = null,
        string? description = null,
        int retryCount = 0,
        double retryDelay = 0.0,
        double retryBackoff = 1.0,
        double? timeout = null,
        bool validateSchema = false,
        double? rateLimit = null,
        ITaskCache? cache = null,
        CircuitBreaker? circuitBreaker = null,
        Type? inputSchemaType = null,
        Type? outputSchemaType = null)
    {
        Id = id ?? $"task_{Guid.NewGuid().ToString("N")[..8]}";
        Description = description ?? $"Task {Id}";
        Execute = execute ?? throw new WaterException("Task must have a callable execute function");
        RetryCount = retryCount;
        RetryDelay = retryDelay;
        RetryBackoff = retryBackoff;
        Timeout = timeout;
        ValidateSchema = validateSchema;
        RateLimit = rateLimit;
        Cache = cache;
        CircuitBreaker = circuitBreaker;
        InputSchemaType = inputSchemaType;
        OutputSchemaType = outputSchemaType;
    }
}

/// <summary>
/// Factory methods for creating tasks.
/// </summary>
public static class TaskFactory
{
    public static WaterTask Create(
        Func<Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>> execute,
        string? id = null,
        string? description = null,
        int retryCount = 0,
        double retryDelay = 0.0,
        double retryBackoff = 1.0,
        double? timeout = null,
        bool validateSchema = false,
        double? rateLimit = null,
        ITaskCache? cache = null,
        CircuitBreaker? circuitBreaker = null)
    {
        return new WaterTask(
            execute, id, description, retryCount, retryDelay,
            retryBackoff, timeout, validateSchema, rateLimit, cache, circuitBreaker);
    }

    /// <summary>
    /// Create a task from a synchronous function.
    /// </summary>
    public static WaterTask CreateSync(
        Func<Dictionary<string, object?>, ExecutionContext, Dictionary<string, object?>> execute,
        string? id = null,
        string? description = null,
        int retryCount = 0,
        double retryDelay = 0.0,
        double retryBackoff = 1.0,
        double? timeout = null)
    {
        return new WaterTask(
            (input, ctx) => System.Threading.Tasks.Task.FromResult(execute(input, ctx)),
            id, description, retryCount, retryDelay, retryBackoff, timeout);
    }
}
