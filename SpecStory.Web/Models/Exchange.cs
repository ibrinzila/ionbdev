namespace SpecStory.Web.Models;

public class Exchange
{
    public string ExchangeId { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public List<Message> Messages { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }

    public int MessageCount => Messages.Count;

    public int ToolUseCount => Messages.Count(m => m.Tool != null);

    public int TotalInputTokens => Messages
        .Where(m => m.Usage != null)
        .Sum(m => m.Usage!.InputTokens);

    public int TotalOutputTokens => Messages
        .Where(m => m.Usage != null)
        .Sum(m => m.Usage!.OutputTokens);
}
