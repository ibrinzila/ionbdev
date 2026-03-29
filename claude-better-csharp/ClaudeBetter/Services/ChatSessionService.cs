using ClaudeBetter.Models;

namespace ClaudeBetter.Services;

public class ChatSessionService
{
    public List<ChatMessage> Messages { get; } = new();

    public string? SystemPrompt { get; set; } = "You are Claude, a helpful AI assistant by Anthropic. Be concise and helpful.";

    public void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = content,
            Timestamp = DateTime.UtcNow
        });
    }

    public ChatMessage AddAssistantPlaceholder()
    {
        var msg = new ChatMessage
        {
            Role = "assistant",
            Content = "",
            Timestamp = DateTime.UtcNow,
            IsStreaming = true
        };
        Messages.Add(msg);
        return msg;
    }

    public void Clear()
    {
        Messages.Clear();
    }
}
