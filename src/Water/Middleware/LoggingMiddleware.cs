using Microsoft.Extensions.Logging;
using Water.Core;
using ExecutionContext = Water.Core.ExecutionContext;

namespace Water.Middleware;

/// <summary>
/// Middleware that logs task_id, input keys, and output keys for every task.
/// </summary>
public class LoggingMiddleware : MiddlewareBase
{
    private readonly ILogger _logger;

    public LoggingMiddleware(ILogger? logger = null, int order = 0) : base(order)
    {
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LoggingMiddleware>();
    }

    public override Task<Dictionary<string, object?>> BeforeTaskAsync(string taskId, Dictionary<string, object?> data, ExecutionContext context)
    {
        _logger.LogInformation("Middleware [before] task={TaskId} input_keys={Keys}", taskId, string.Join(", ", data.Keys));
        return Task.FromResult(data);
    }

    public override Task<Dictionary<string, object?>> AfterTaskAsync(string taskId, Dictionary<string, object?> data, Dictionary<string, object?> result, ExecutionContext context)
    {
        _logger.LogInformation("Middleware [after]  task={TaskId} output_keys={Keys}", taskId, string.Join(", ", result.Keys));
        return Task.FromResult(result);
    }
}
