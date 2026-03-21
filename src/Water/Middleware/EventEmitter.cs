using System.Threading.Channels;

namespace Water.Middleware;

/// <summary>
/// Represents a single event emitted during flow execution.
/// </summary>
public class FlowEvent
{
    public string EventType { get; set; }
    public string FlowId { get; set; }
    public string? TaskId { get; set; }
    public string? ExecutionId { get; set; }
    public Dictionary<string, object?> Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public FlowEvent(string eventType, string flowId, string? taskId = null, string? executionId = null, Dictionary<string, object?>? data = null)
    {
        EventType = eventType;
        FlowId = flowId;
        TaskId = taskId;
        ExecutionId = executionId;
        Data = data ?? new();
    }

    public Dictionary<string, object?> ToDict() => new()
    {
        ["event_type"] = EventType,
        ["flow_id"] = FlowId,
        ["task_id"] = TaskId,
        ["execution_id"] = ExecutionId,
        ["data"] = Data,
        ["timestamp"] = Timestamp.ToString("O")
    };
}

/// <summary>
/// Async event emitter for real-time flow execution updates.
/// </summary>
public class EventEmitter
{
    private readonly List<Channel<FlowEvent?>> _channels = new();
    private bool _closed;

    public async Task EmitAsync(FlowEvent evt)
    {
        foreach (var channel in _channels)
        {
            if (!channel.Writer.TryWrite(evt))
                Console.Error.WriteLine($"Event channel full, dropping event: {evt.EventType}");
        }
        await Task.CompletedTask;
    }

    public EventSubscription Subscribe(int maxQueueSize = 1000)
    {
        var channel = Channel.CreateBounded<FlowEvent?>(maxQueueSize);
        _channels.Add(channel);
        return new EventSubscription(channel, this);
    }

    internal void Unsubscribe(Channel<FlowEvent?> channel)
    {
        _channels.Remove(channel);
    }

    public async Task CloseAsync()
    {
        _closed = true;
        foreach (var channel in _channels)
            channel.Writer.TryWrite(null); // sentinel
        await Task.CompletedTask;
    }

    public int SubscriberCount => _channels.Count;
}

/// <summary>
/// Subscription to flow events.
/// </summary>
public class EventSubscription : IAsyncDisposable
{
    private readonly Channel<FlowEvent?> _channel;
    private readonly EventEmitter _emitter;

    public EventSubscription(Channel<FlowEvent?> channel, EventEmitter emitter)
    {
        _channel = channel;
        _emitter = emitter;
    }

    public async IAsyncEnumerable<FlowEvent> ReadAllAsync()
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync())
        {
            if (evt is null) yield break;
            yield return evt;
        }
    }

    public async Task<FlowEvent?> GetAsync(TimeSpan? timeout = null)
    {
        try
        {
            if (timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(timeout.Value);
                return await _channel.Reader.ReadAsync(cts.Token);
            }
            return await _channel.Reader.ReadAsync();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        _emitter.Unsubscribe(_channel);
        return ValueTask.CompletedTask;
    }
}
