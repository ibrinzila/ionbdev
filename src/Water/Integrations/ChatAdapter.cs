using Water.Core;

namespace Water.Integrations;

/// <summary>
/// A single chat message.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Interface for chat platform adapters.
/// </summary>
public interface IChatAdapter
{
    Task SendMessageAsync(string channel, string message);
    Task<ChatMessage?> ReceiveMessageAsync(CancellationToken ct = default);
}

/// <summary>
/// In-memory chat adapter for testing.
/// </summary>
public class InMemoryChatAdapter : IChatAdapter
{
    private readonly Queue<ChatMessage> _inbox = new();
    private readonly List<(string Channel, string Message)> _outbox = new();

    public Task SendMessageAsync(string channel, string message)
    {
        _outbox.Add((channel, message));
        return Task.CompletedTask;
    }

    public Task<ChatMessage?> ReceiveMessageAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_inbox.Count > 0 ? _inbox.Dequeue() : null);
    }

    public void EnqueueIncoming(ChatMessage message) => _inbox.Enqueue(message);
    public IReadOnlyList<(string Channel, string Message)> SentMessages => _outbox.AsReadOnly();
}

/// <summary>
/// Chat bot that wraps a flow for conversational interactions.
/// </summary>
public class ChatBot
{
    private readonly Flow _flow;
    private readonly IChatAdapter _adapter;
    private readonly List<ChatMessage> _history = new();

    public ChatBot(Flow flow, IChatAdapter adapter)
    {
        _flow = flow;
        _adapter = adapter;
    }

    public async Task<string> ProcessMessageAsync(string userMessage, string channel = "default")
    {
        _history.Add(new ChatMessage("user", userMessage));

        var result = await _flow.RunAsync(new Dictionary<string, object?>
        {
            ["prompt"] = userMessage,
            ["history"] = _history.Select(m => new Dictionary<string, object?> { ["role"] = m.Role, ["content"] = m.Content }).ToList()
        });

        var response = result.TryGetValue("response", out var r) ? r?.ToString() ?? "" : "";
        _history.Add(new ChatMessage("assistant", response));
        await _adapter.SendMessageAsync(channel, response);

        return response;
    }

    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();
}
