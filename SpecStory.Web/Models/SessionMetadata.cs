namespace SpecStory.Web.Models;

public class SessionMetadata
{
    public string SessionId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? CreatedAt { get; set; }
    public string? WorkspaceRoot { get; set; }
    public string? FilePath { get; set; }
}
