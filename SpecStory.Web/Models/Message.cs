namespace SpecStory.Web.Models;

public class Message
{
    public string Id { get; set; } = string.Empty;
    public string? Timestamp { get; set; }

    /// <summary>
    /// "user" or "agent"
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public string? Model { get; set; }
    public List<ContentPart> Content { get; set; } = new();
    public ToolInfo? Tool { get; set; }
    public List<string>? PathHints { get; set; }
    public Usage? Usage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public string TextContent => string.Join("\n", Content
        .Where(c => c.Type == "text")
        .Select(c => c.Text));
}
