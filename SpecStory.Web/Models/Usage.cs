namespace SpecStory.Web.Models;

public class Usage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int? CacheCreationInputTokens { get; set; }
    public int? CacheReadInputTokens { get; set; }
    public int? CachedInputTokens { get; set; }
    public int? ReasoningOutputTokens { get; set; }
    public int? ThoughtTokens { get; set; }
    public int? ToolTokens { get; set; }

    public int TotalTokens => InputTokens + OutputTokens;
}
