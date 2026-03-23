using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

/// <summary>
/// REST API controller for programmatic access to SpecStory data.
/// </summary>
[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IProviderService _providerService;
    private readonly IMarkdownService _markdownService;
    private readonly ICloudSyncService _cloudSyncService;

    public ApiController(
        ISessionService sessionService,
        IProviderService providerService,
        IMarkdownService markdownService,
        ICloudSyncService cloudSyncService)
    {
        _sessionService = sessionService;
        _providerService = providerService;
        _markdownService = markdownService;
        _cloudSyncService = cloudSyncService;
    }

    /// <summary>
    /// List all sessions with optional provider filter.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions(
        [FromQuery] string? provider = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var sessions = await _sessionService.ListSessionsAsync(provider);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sessions = sessions
                .Where(s => (s.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            s.SessionId.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = sessions.Count;
        var paged = sessions.Skip((page - 1) * pageSize).Take(pageSize);

        return Ok(new
        {
            total,
            page,
            pageSize,
            sessions = paged
        });
    }

    /// <summary>
    /// Get a specific session by ID.
    /// </summary>
    [HttpGet("sessions/{id}")]
    public async Task<IActionResult> GetSession(string id, [FromQuery] string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        return Ok(session);
    }

    /// <summary>
    /// Get session as markdown.
    /// </summary>
    [HttpGet("sessions/{id}/markdown")]
    public async Task<IActionResult> GetSessionMarkdown(string id, [FromQuery] string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        var markdown = _markdownService.GenerateMarkdown(session);
        return Content(markdown, "text/markdown");
    }

    /// <summary>
    /// Get session statistics.
    /// </summary>
    [HttpGet("sessions/{id}/stats")]
    public async Task<IActionResult> GetSessionStats(string id, [FromQuery] string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        var stats = _sessionService.CalculateStatistics(session);
        return Ok(stats);
    }

    /// <summary>
    /// Check all provider installations.
    /// </summary>
    [HttpGet("providers")]
    public IActionResult ListProviders()
    {
        var providers = _providerService.CheckAllProviders();
        return Ok(providers);
    }

    /// <summary>
    /// Check a specific provider installation.
    /// </summary>
    [HttpGet("providers/{id}")]
    public IActionResult CheckProvider(string id)
    {
        var result = _providerService.CheckProvider(id);
        return Ok(result);
    }

    /// <summary>
    /// Trigger cloud sync for all sessions.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncAll([FromQuery] string? provider = null)
    {
        var result = await _cloudSyncService.SyncAllSessionsAsync(provider);
        return Ok(result);
    }

    /// <summary>
    /// Trigger cloud sync for a specific session.
    /// </summary>
    [HttpPost("sync/{id}")]
    public async Task<IActionResult> SyncSession(string id, [FromQuery] string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound(new { error = $"Session '{id}' not found" });

        var result = await _cloudSyncService.SyncSessionAsync(
            session,
            session.WorkspaceRoot ?? "unknown",
            Path.GetFileName(session.WorkspaceRoot ?? "unknown"));

        return Ok(result);
    }

    /// <summary>
    /// Get authentication status.
    /// </summary>
    [HttpGet("auth/status")]
    public async Task<IActionResult> AuthStatus()
    {
        var isAuth = await _cloudSyncService.IsAuthenticatedAsync();
        return Ok(new { authenticated = isAuth });
    }
}
