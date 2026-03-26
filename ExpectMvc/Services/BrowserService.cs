using System.Text;
using System.Text.Json;
using ExpectMvc.Models;
using Microsoft.Playwright;

namespace ExpectMvc.Services;

public class BrowserService : IBrowserService
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
        _logger.LogInformation("Capturing accessibility snapshot");

        var tree = await session.Page.Accessibility.SnapshotAsync();
        var refs = new Dictionary<string, RefEntry>();
        var sb = new StringBuilder();
        var refCounter = 0;

        void WalkTree(JsonElement node, int depth)
        {
            if (depth > options.MaxDepth) return;

            var role = node.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
            var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var indent = new string(' ', depth * 2);

            var isInteractive = IsInteractiveRole(role);

            if (!options.InteractiveOnly || isInteractive)
            {
                var refId = $"e{++refCounter}";
                refs[refId] = new RefEntry { Role = role, Name = name };

                if (options.Compact && string.IsNullOrEmpty(name) && !isInteractive)
                    return;

                sb.AppendLine($"{indent}[{refId}] {role} \"{name}\"");
            }

            if (node.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    WalkTree(child, depth + 1);
                }
            }
        }

        if (tree != null)
        {
            var json = JsonSerializer.Serialize(tree);
            var doc = JsonDocument.Parse(json);
            WalkTree(doc.RootElement, 0);
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

        // Build a locator from the accessibility role/name
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
        await session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return await SnapshotAsync(session);
    }

    public async Task CloseAsync(BrowserSession session)
    {
        await session.DisposeAsync();
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
