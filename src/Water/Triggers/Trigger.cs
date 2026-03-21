using Water.Core;

namespace Water.Triggers;

/// <summary>
/// Event data from a trigger.
/// </summary>
public class TriggerEvent
{
    public string TriggerId { get; set; }
    public string TriggerType { get; set; }
    public Dictionary<string, object?> Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public TriggerEvent(string triggerId, string triggerType, Dictionary<string, object?> data)
    {
        TriggerId = triggerId;
        TriggerType = triggerType;
        Data = data;
    }
}

/// <summary>
/// Base interface for triggers that start flow execution.
/// </summary>
public interface ITrigger
{
    string Id { get; }
    string Type { get; }
    Task StartAsync(Func<TriggerEvent, Task> handler, CancellationToken cancellationToken = default);
    Task StopAsync();
}

/// <summary>
/// Webhook trigger that listens for HTTP requests.
/// </summary>
public class WebhookTrigger : ITrigger
{
    public string Id { get; }
    public string Type => "webhook";
    public string Path { get; }

    private Func<TriggerEvent, Task>? _handler;

    public WebhookTrigger(string path, string? id = null)
    {
        Path = path;
        Id = id ?? $"webhook_{Guid.NewGuid().ToString("N")[..8]}";
    }

    public Task StartAsync(Func<TriggerEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        _handler = handler;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _handler = null;
        return Task.CompletedTask;
    }

    public async Task FireAsync(Dictionary<string, object?> data)
    {
        if (_handler != null)
            await _handler(new TriggerEvent(Id, Type, data));
    }
}

/// <summary>
/// Cron-based trigger that fires on a schedule.
/// </summary>
public class CronTrigger : ITrigger
{
    public string Id { get; }
    public string Type => "cron";
    public string Expression { get; }

    private CancellationTokenSource? _cts;

    public CronTrigger(string expression, string? id = null)
    {
        Expression = expression;
        Id = id ?? $"cron_{Guid.NewGuid().ToString("N")[..8]}";
    }

    public async Task StartAsync(Func<TriggerEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Simplified: fires every interval parsed from expression
        // A full implementation would parse cron expressions
        await Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Queue-based trigger that fires when messages arrive.
/// </summary>
public class QueueTrigger : ITrigger
{
    public string Id { get; }
    public string Type => "queue";
    public string QueueName { get; }

    private Func<TriggerEvent, Task>? _handler;

    public QueueTrigger(string queueName, string? id = null)
    {
        QueueName = queueName;
        Id = id ?? $"queue_{Guid.NewGuid().ToString("N")[..8]}";
    }

    public Task StartAsync(Func<TriggerEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        _handler = handler;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _handler = null;
        return Task.CompletedTask;
    }

    public async Task EnqueueAsync(Dictionary<string, object?> data)
    {
        if (_handler != null)
            await _handler(new TriggerEvent(Id, Type, data));
    }
}

/// <summary>
/// Registry for managing triggers.
/// </summary>
public class TriggerRegistry
{
    private readonly Dictionary<string, ITrigger> _triggers = new();

    public TriggerRegistry Register(ITrigger trigger)
    {
        _triggers[trigger.Id] = trigger;
        return this;
    }

    public ITrigger? Get(string id) => _triggers.TryGetValue(id, out var t) ? t : null;

    public IReadOnlyList<ITrigger> All => _triggers.Values.ToList();

    public async Task StartAllAsync(Func<TriggerEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        foreach (var trigger in _triggers.Values)
            await trigger.StartAsync(handler, cancellationToken);
    }

    public async Task StopAllAsync()
    {
        foreach (var trigger in _triggers.Values)
            await trigger.StopAsync();
    }
}
