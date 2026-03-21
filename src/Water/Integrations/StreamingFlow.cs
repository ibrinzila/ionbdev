using System.Threading.Channels;

namespace Water.Integrations;

/// <summary>
/// Represents a streaming event from flow execution.
/// </summary>
public class StreamEvent
{
    public string EventType { get; set; }
    public string FlowId { get; set; }
    public string? TaskId { get; set; }
    public Dictionary<string, object?> Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public StreamEvent(string eventType, string flowId, string? taskId = null, Dictionary<string, object?>? data = null)
    {
        EventType = eventType;
        FlowId = flowId;
        TaskId = taskId;
        Data = data ?? new();
    }
}

/// <summary>
/// Manages streaming flow subscriptions.
/// </summary>
public class StreamManager
{
    private readonly Dictionary<string, Channel<StreamEvent>> _channels = new();

    public Channel<StreamEvent> Subscribe(string executionId)
    {
        var channel = Channel.CreateBounded<StreamEvent>(1000);
        _channels[executionId] = channel;
        return channel;
    }

    public void Unsubscribe(string executionId)
    {
        if (_channels.TryGetValue(executionId, out var channel))
        {
            channel.Writer.TryComplete();
            _channels.Remove(executionId);
        }
    }

    public async Task EmitAsync(string executionId, StreamEvent evt)
    {
        if (_channels.TryGetValue(executionId, out var channel))
            await channel.Writer.WriteAsync(evt);
    }
}
