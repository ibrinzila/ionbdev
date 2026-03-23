using System.Text.Json;

namespace SpecStory.Web.Models;

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tool type: read, write, search, shell, task, generic, unknown
    /// </summary>
    public string Type { get; set; } = "unknown";

    public string? UseId { get; set; }
    public Dictionary<string, JsonElement>? Input { get; set; }
    public Dictionary<string, JsonElement>? Output { get; set; }
    public string? Summary { get; set; }
    public string? FormattedMarkdown { get; set; }
}
