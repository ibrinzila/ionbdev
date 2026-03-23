namespace SpecStory.Web.Models;

public class SessionData
{
    public string SchemaVersion { get; set; } = "1";
    public ProviderInfo Provider { get; set; } = new();
    public string SessionId { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Slug { get; set; }
    public string? WorkspaceRoot { get; set; }
    public List<Exchange> Exchanges { get; set; } = new();

    public string DisplayName => !string.IsNullOrEmpty(Slug)
        ? Slug.Replace("-", " ")
        : SessionId;

    public int TotalMessages => Exchanges.Sum(e => e.MessageCount);

    public int TotalToolUses => Exchanges.Sum(e => e.ToolUseCount);

    public int TotalInputTokens => Exchanges.Sum(e => e.TotalInputTokens);

    public int TotalOutputTokens => Exchanges.Sum(e => e.TotalOutputTokens);

    public int TotalTokens => TotalInputTokens + TotalOutputTokens;

    public TimeSpan? Duration
    {
        get
        {
            if (DateTime.TryParse(CreatedAt, out var start) &&
                DateTime.TryParse(UpdatedAt, out var end))
                return end - start;
            return null;
        }
    }
}
