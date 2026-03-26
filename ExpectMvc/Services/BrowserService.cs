using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using ExpectMvc.Models;
using Microsoft.Playwright;

namespace ExpectMvc.Services;

public class BrowserService : IBrowserService, IAsyncDisposable
{
    private readonly ICookieService _cookies;
    private readonly ILogger<BrowserService> _logger;
    private IPlaywright? _playwright;

    public BrowserService(ICookieService cookies, ILogger<BrowserService> logger)
    {
        _cookies = cookies;
        _logger = logger;
    }

    public async Task<BrowserSession> CreatePageAsync(string url, BrowserOptions? options = null)
    {
        options ??= new BrowserOptions();
        _playwright ??= await Playwright.CreateAsync();

        _logger.LogInformation("Launching browser for {Url} (headed={Headed})", url, options.Headed);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = !options.Headed,
            ExecutablePath = options.ExecutablePath
        };

        var browser = await _playwright.Chromium.LaunchAsync(launchOptions);

        var contextOptions = new BrowserNewContextOptions();
        if (options.RecordVideo)
        {
            contextOptions.RecordVideoDir = Path.Combine(Path.GetTempPath(), "expect-videos");
        }

        var context = await browser.NewContextAsync(contextOptions);

        if (options.InjectCookies)
        {
            var uri = new Uri(url);
            var cookies = await _cookies.ExtractCookiesAsync();
            var pwCookies = _cookies.ToPlaywrightCookies(cookies, uri.Host);
            if (pwCookies.Count > 0)
            {
                await context.AddCookiesAsync(pwCookies);
            }
        }

        var page = await context.NewPageAsync();

        var waitUntil = options.WaitUntil.ToLowerInvariant() switch
        {
            "networkidle" => WaitUntilState.NetworkIdle,
            "domcontentloaded" => WaitUntilState.DOMContentLoaded,
            "commit" => WaitUntilState.Commit,
            _ => WaitUntilState.Load
        };

        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = waitUntil });

        var session = new BrowserSession
        {
            Browser = browser,
            Context = context,
            Page = page
        };

        if (options.RecordVideo && page.Video != null)
        {
            session.VideoPath = await page.Video.PathAsync();
        }

        return session;
    }

    public async Task<SnapshotResult> SnapshotAsync(BrowserSession session, SnapshotOptions? options = null)
    {
        options ??= new SnapshotOptions();
        _logger.LogInformation("Capturing accessibility snapshot via AriaSnapshot");

        // Use Playwright's modern AriaSnapshot API (Locator.AriaSnapshotAsync)
        // which replaced the deprecated page.Accessibility.SnapshotAsync().
        var ariaYaml = await session.Page.Locator("body").AriaSnapshotAsync(
            new LocatorAriaSnapshotOptions { Timeout = options.TimeoutMs });

        var refs = new Dictionary<string, RefEntry>();
        var sb = new StringBuilder();
        var refCounter = 0;

        // AriaSnapshotAsync returns a YAML-like string describing the accessibility tree:
        //   - role "name":
        //     - childrole "childname"
        // Parse each line to extract role + name and assign ref IDs.
        foreach (var rawLine in ariaYaml.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var depth = (rawLine.Length - rawLine.TrimStart().Length) / 2;
            if (depth > options.MaxDepth) continue;

            var trimmed = rawLine.Trim().TrimStart('-').Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Pattern: role "name" or just role
            var match = Regex.Match(trimmed, @"^(\w+)(?:\s+""([^""]*)"")?\s*:?\s*$");
            if (!match.Success) continue;

            var role = match.Groups[1].Value;
            var name = match.Groups[2].Success ? match.Groups[2].Value : "";
            var isInteractive = IsInteractiveRole(role);

            if (options.InteractiveOnly && !isInteractive) continue;
            if (options.Compact && string.IsNullOrEmpty(name) && !isInteractive) continue;

            var refId = $"e{++refCounter}";
            refs[refId] = new RefEntry { Role = role, Name = name };

            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}[{refId}] {role} \"{name}\"");
        }

        return new SnapshotResult
        {
            Tree = sb.ToString(),
            Refs = refs
        };
    }

    public async Task<SnapshotResult> ActAsync(BrowserSession session, string refId, string action, string? value = null)
    {
        _logger.LogInformation("Acting on {RefId}: {Action}", refId, action);

        // Take a fresh snapshot to get current ref-to-element mapping
        var snapshot = await SnapshotAsync(session);
        if (!snapshot.Refs.TryGetValue(refId, out var entry))
            throw new InvalidOperationException($"Ref '{refId}' not found in current snapshot");

        var locator = session.Page.GetByRole(MapRole(entry.Role), new PageGetByRoleOptions { Name = entry.Name });

        if (entry.Nth.HasValue)
            locator = locator.Nth(entry.Nth.Value);

        switch (action.ToLowerInvariant())
        {
            case "click":
                await locator.ClickAsync();
                break;
            case "fill":
                await locator.FillAsync(value ?? "");
                break;
            case "type":
                await locator.PressSequentiallyAsync(value ?? "");
                break;
            case "check":
                await locator.CheckAsync();
                break;
            case "uncheck":
                await locator.UncheckAsync();
                break;
            case "select":
                await locator.SelectOptionAsync(value ?? "");
                break;
            case "hover":
                await locator.HoverAsync();
                break;
            case "focus":
                await locator.FocusAsync();
                break;
            default:
                throw new ArgumentException($"Unknown action: {action}");
        }

        // Wait for any navigation/network activity to settle
        try
        {
            await session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // NetworkIdle may not always fire; continue with snapshot anyway
        }

        return await SnapshotAsync(session);
    }

    public async Task CloseAsync(BrowserSession session)
    {
        await session.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _playwright?.Dispose();
        _playwright = null;
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }

    private static bool IsInteractiveRole(string role) =>
        role is "button" or "link" or "textbox" or "checkbox" or "radio"
            or "combobox" or "listbox" or "menuitem" or "tab" or "switch"
            or "slider" or "spinbutton" or "searchbox" or "option";

    private static AriaRole MapRole(string role) => role.ToLowerInvariant() switch
    {
        "button" => AriaRole.Button,
        "link" => AriaRole.Link,
        "textbox" => AriaRole.Textbox,
        "checkbox" => AriaRole.Checkbox,
        "radio" => AriaRole.Radio,
        "combobox" => AriaRole.Combobox,
        "listbox" => AriaRole.Listbox,
        "menuitem" => AriaRole.Menuitem,
        "tab" => AriaRole.Tab,
        "heading" => AriaRole.Heading,
        "img" => AriaRole.Img,
        "list" => AriaRole.List,
        "listitem" => AriaRole.Listitem,
        "navigation" => AriaRole.Navigation,
        "region" => AriaRole.Region,
        _ => AriaRole.Generic
    };
}
