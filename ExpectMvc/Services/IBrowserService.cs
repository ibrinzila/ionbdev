using ExpectMvc.Models;

namespace ExpectMvc.Services;

/// <summary>
/// Browser automation — mirrors @expect/browser.
/// Launches Playwright, captures accessibility snapshots, and executes ref-based actions.
/// </summary>
public interface IBrowserService
{
    Task<BrowserSession> CreatePageAsync(string url, BrowserOptions? options = null);
    Task<SnapshotResult> SnapshotAsync(BrowserSession session, SnapshotOptions? options = null);
    Task<SnapshotResult> ActAsync(BrowserSession session, string refId, string action, string? value = null);
    Task CloseAsync(BrowserSession session);
}

public class BrowserSession : IAsyncDisposable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Microsoft.Playwright.IBrowser Browser { get; set; } = null!;
    public Microsoft.Playwright.IBrowserContext Context { get; set; } = null!;
    public Microsoft.Playwright.IPage Page { get; set; } = null!;
    public string? VideoPath { get; set; }

    public async ValueTask DisposeAsync()
    {
        await Browser.CloseAsync();
        GC.SuppressFinalize(this);
    }
}

public class BrowserOptions
{
    public bool Headed { get; set; }
    public string? ExecutablePath { get; set; }
    public bool InjectCookies { get; set; }
    public string WaitUntil { get; set; } = "load";
    public bool RecordVideo { get; set; }
}

public class SnapshotOptions
{
    public int TimeoutMs { get; set; } = 30000;
    public bool InteractiveOnly { get; set; } = true;
    public bool Compact { get; set; } = true;
    public int MaxDepth { get; set; } = 10;
}
