using Microsoft.Playwright;

namespace ExpectMvc.Services;

/// <summary>
/// Cookie extraction and injection — mirrors @expect/cookies.
/// Extracts cookies from browser profiles for authenticated testing.
/// </summary>
public interface ICookieService
{
    Task<IReadOnlyList<BrowserCookie>> ExtractCookiesAsync(string browserProfile = "Default");
    IReadOnlyList<Cookie> ToPlaywrightCookies(IEnumerable<BrowserCookie> cookies, string domain);
    string ToCookieHeader(IEnumerable<BrowserCookie> cookies);
}

public class BrowserCookie
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public bool Secure { get; set; }
    public bool HttpOnly { get; set; }
    public double? Expires { get; set; }
    public SameSiteAttribute SameSite { get; set; } = SameSiteAttribute.None;
}

public enum SameSiteAttribute
{
    None,
    Lax,
    Strict
}
