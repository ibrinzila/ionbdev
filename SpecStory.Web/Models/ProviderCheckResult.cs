namespace SpecStory.Web.Models;

public class ProviderCheckResult
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string? Version { get; set; }
    public string? Location { get; set; }
    public string? Error { get; set; }
}
