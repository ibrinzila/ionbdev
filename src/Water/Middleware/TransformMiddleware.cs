using Water.Core;
using ExecutionContext = Water.Core.ExecutionContext;

namespace Water.Middleware;

/// <summary>
/// Middleware that delegates to user-supplied functions for custom transforms.
/// </summary>
public class TransformMiddleware : MiddlewareBase
{
    private readonly Func<string, Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>>? _beforeFn;
    private readonly Func<string, Dictionary<string, object?>, Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>>? _afterFn;

    public TransformMiddleware(
        Func<string, Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>>? beforeFn = null,
        Func<string, Dictionary<string, object?>, Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>>? afterFn = null,
        int order = 0) : base(order)
    {
        _beforeFn = beforeFn;
        _afterFn = afterFn;
    }

    public override async Task<Dictionary<string, object?>> BeforeTaskAsync(string taskId, Dictionary<string, object?> data, ExecutionContext context)
    {
        return _beforeFn != null ? await _beforeFn(taskId, data, context) : data;
    }

    public override async Task<Dictionary<string, object?>> AfterTaskAsync(string taskId, Dictionary<string, object?> data, Dictionary<string, object?> result, ExecutionContext context)
    {
        return _afterFn != null ? await _afterFn(taskId, data, result, context) : result;
    }
}
