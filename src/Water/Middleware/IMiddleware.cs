using Water.Core;

namespace Water.Middleware;

/// <summary>
/// Base middleware interface. Middleware intercepts task execution for transformation or logging.
/// </summary>
public interface IMiddleware
{
    int Order { get; }
    Task<Dictionary<string, object?>> BeforeTaskAsync(string taskId, Dictionary<string, object?> data, ExecutionContext context);
    Task<Dictionary<string, object?>> AfterTaskAsync(string taskId, Dictionary<string, object?> data, Dictionary<string, object?> result, ExecutionContext context);
}

/// <summary>
/// Base middleware class with no-op defaults.
/// </summary>
public abstract class MiddlewareBase : IMiddleware
{
    public int Order { get; set; }

    protected MiddlewareBase(int order = 0) => Order = order;

    public virtual Task<Dictionary<string, object?>> BeforeTaskAsync(string taskId, Dictionary<string, object?> data, ExecutionContext context)
        => Task.FromResult(data);

    public virtual Task<Dictionary<string, object?>> AfterTaskAsync(string taskId, Dictionary<string, object?> data, Dictionary<string, object?> result, ExecutionContext context)
        => Task.FromResult(result);
}
