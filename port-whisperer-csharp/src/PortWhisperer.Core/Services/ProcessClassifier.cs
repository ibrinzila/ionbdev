using System.Text.RegularExpressions;

namespace PortWhisperer.Core.Services;

public static class ProcessClassifier
{
    private static readonly HashSet<string> SystemApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "spotify", "raycast", "tableplus", "postman", "linear", "cursor",
        "controlce", "rapportd", "superhuma", "setappage", "slack", "discord",
        "firefox", "chrome", "google", "safari", "figma", "notion", "zoom",
        "teams", "code", "iterm2", "warp", "arc", "loginwindow", "windowserver",
        "systemuise", "kernel_task", "launchd", "mdworker", "mds_stores",
        "cfprefsd", "coreaudio", "corebrightne", "airportd", "bluetoothd",
        "sharingd", "usernoted", "notificationc", "cloudd",
    };

    private static readonly HashSet<string> DevNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "python", "python3", "ruby", "java", "go", "cargo", "deno",
        "bun", "php", "uvicorn", "gunicorn", "flask", "rails", "npm", "npx",
        "yarn", "pnpm", "tsc", "tsx", "esbuild", "rollup", "turbo", "nx",
        "jest", "vitest", "mocha", "pytest", "cypress", "playwright", "rustc",
        "dotnet", "gradle", "mvn", "mix", "elixir",
    };

    private static readonly Regex[] CmdIndicators =
    [
        new(@"\bnode\b", RegexOptions.Compiled),
        new(@"\bnext[\s-]", RegexOptions.Compiled),
        new(@"\bvite\b", RegexOptions.Compiled),
        new(@"\bnuxt\b", RegexOptions.Compiled),
        new(@"\bwebpack\b", RegexOptions.Compiled),
        new(@"\bremix\b", RegexOptions.Compiled),
        new(@"\bastro\b", RegexOptions.Compiled),
        new(@"\bgulp\b", RegexOptions.Compiled),
        new(@"\bng serve\b", RegexOptions.Compiled),
        new(@"\bgatsb", RegexOptions.Compiled),
        new(@"\bflask\b", RegexOptions.Compiled),
        new(@"\bdjango\b|manage\.py", RegexOptions.Compiled),
        new(@"\buvicorn\b", RegexOptions.Compiled),
        new(@"\brails\b", RegexOptions.Compiled),
        new(@"\bcargo\b", RegexOptions.Compiled),
        new(@"\bdotnet\b", RegexOptions.Compiled),
    ];

    public static bool IsDevProcess(string? processName, string? command)
    {
        var name = (processName ?? "").ToLowerInvariant();
        var cmd = (command ?? "").ToLowerInvariant();

        // Known system/desktop apps
        foreach (var app in SystemApps)
        {
            if (name.StartsWith(app, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Dev process names
        if (DevNames.Contains(name)) return true;

        // Docker
        if (name.StartsWith("com.docke") || name is "docker" or "docker-sandbox")
            return true;

        // Command-line indicators
        foreach (var re in CmdIndicators)
        {
            if (re.IsMatch(cmd)) return true;
        }

        return false;
    }

    public static bool IsDockerProcess(string processName)
    {
        return processName.StartsWith("com.docke") ||
               processName.StartsWith("Docker") ||
               processName is "docker" or "docker-sandbox";
    }
}
