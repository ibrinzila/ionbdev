namespace SpecStory.Web.Models.ViewModels;

public class SessionDetailViewModel
{
    public SessionData Session { get; set; } = new();
    public string MarkdownContent { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public SessionStatistics Statistics { get; set; } = new();
}

public class SessionStatistics
{
    public int ExchangeCount { get; set; }
    public int MessageCount { get; set; }
    public int UserMessageCount { get; set; }
    public int AgentMessageCount { get; set; }
    public int ToolUseCount { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public TimeSpan? Duration { get; set; }
    public Dictionary<string, int> ToolUsageByType { get; set; } = new();
    public List<string> ModelsUsed { get; set; } = new();
    public List<string> FilesReferenced { get; set; } = new();
}
