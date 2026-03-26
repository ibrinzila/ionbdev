using Microsoft.Playwright;

namespace ExpectMvc.Services;

public class CookieService : ICookieService
{
    private readonly ILogger<CookieService> _logger;

    public CookieService(ILogger<CookieService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BrowserCookie>> ExtractCookiesAsync(string browserProfile = "Default")
    {
        _logger.LogInformation("Extracting cookies from profile: {Profile}", browserProfile);

        // Locate Chrome/Chromium cookie database based on platform
        var cookiePath = GetCookieDatabasePath(browserProfile);
        if (cookiePath == null || !File.Exists(cookiePath))
        {
            _logger.LogWarning("Cookie database not found at expected path");
            return Array.Empty<BrowserCookie>();
        }

        // In production, this would decrypt and read the SQLite cookie DB.
        // Chromium cookies are stored encrypted; decryption varies by OS.
        await Task.CompletedTask;
        _logger.LogInformation("Cookie extraction from local browser profiles requires platform-specific decryption");
        return Array.Empty<BrowserCookie>();
    }

    public IReadOnlyList<Cookie> ToPlaywrightCookies(IEnumerable<BrowserCookie> cookies, string domain)
    {
        return cookies
            .Where(c => c.Domain == domain || c.Domain.EndsWith("." + domain))
            .Select(c => new Cookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Secure = c.Secure,
                HttpOnly = c.HttpOnly,
                Expires = (float)(c.Expires ?? -1),
                SameSite = c.SameSite switch
                {
                    SameSiteAttribute.Strict => Microsoft.Playwright.SameSiteAttribute.Strict,
                    SameSiteAttribute.Lax => Microsoft.Playwright.SameSiteAttribute.Lax,
                    _ => Microsoft.Playwright.SameSiteAttribute.None
                }
            })
            .ToList();
    }

    public string ToCookieHeader(IEnumerable<BrowserCookie> cookies)
    {
        return string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
    }

    private static string? GetCookieDatabasePath(string profile)
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "User Data", profile, "Network", "Cookies");

        if (OperatingSystem.IsMacOS())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Google", "Chrome", profile, "Cookies");

        // Linux
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "google-chrome", profile, "Cookies");
    }
}
