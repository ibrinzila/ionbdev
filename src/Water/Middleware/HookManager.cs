namespace Water.Middleware;

/// <summary>
/// Manages lifecycle hooks for flow and task execution.
/// </summary>
public class HookManager
{
    private static readonly string[] ValidEvents =
    {
        "on_task_start", "on_task_complete", "on_task_error",
        "on_flow_start", "on_flow_complete", "on_flow_error"
    };

    private readonly Dictionary<string, List<Func<Dictionary<string, object?>, Task>>> _hooks = new();

    public HookManager()
    {
        foreach (var evt in ValidEvents)
            _hooks[evt] = new();
    }

    /// <summary>
    /// Register a hook callback for an event.
    /// </summary>
    public void On(string eventName, Func<Dictionary<string, object?>, Task> callback)
    {
        if (!_hooks.ContainsKey(eventName))
            throw new ArgumentException($"Unknown hook event: {eventName}. Valid events: {string.Join(", ", ValidEvents)}");
        _hooks[eventName].Add(callback);
    }

    /// <summary>
    /// Register a synchronous hook callback.
    /// </summary>
    public void On(string eventName, Action<Dictionary<string, object?>> callback)
    {
        On(eventName, args =>
        {
            callback(args);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Fire all registered callbacks for an event.
    /// </summary>
    public async Task EmitAsync(string eventName, Dictionary<string, object?>? args = null)
    {
        if (!_hooks.TryGetValue(eventName, out var callbacks))
            return;

        var eventArgs = args ?? new();
        foreach (var callback in callbacks)
        {
            try
            {
                await callback(eventArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Hook {eventName} callback raised: {ex.Message}");
            }
        }
    }

    public bool HasHooks(string eventName) => _hooks.TryGetValue(eventName, out var callbacks) && callbacks.Count > 0;
}
