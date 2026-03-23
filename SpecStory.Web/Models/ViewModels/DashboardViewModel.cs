namespace SpecStory.Web.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalSessions { get; set; }
    public int TotalExchanges { get; set; }
    public int TotalToolUses { get; set; }
    public long TotalTokens { get; set; }
    public List<ProviderCheckResult> Providers { get; set; } = new();
    public List<SessionMetadata> RecentSessions { get; set; } = new();
    public CloudSyncResult? LastSyncResult { get; set; }
    public Dictionary<string, int> SessionsByProvider { get; set; } = new();
}
