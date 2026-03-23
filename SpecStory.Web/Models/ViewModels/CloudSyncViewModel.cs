namespace SpecStory.Web.Models.ViewModels;

public class CloudSyncViewModel
{
    public bool IsAuthenticated { get; set; }
    public string? UserEmail { get; set; }
    public string CloudUrl { get; set; } = string.Empty;
    public bool SyncEnabled { get; set; }
    public CloudSyncResult? LastSyncResult { get; set; }
    public List<SyncHistoryEntry> SyncHistory { get; set; } = new();
    public string? StatusMessage { get; set; }
}

public class SyncHistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}
