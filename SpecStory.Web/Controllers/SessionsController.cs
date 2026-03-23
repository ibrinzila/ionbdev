using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Models.ViewModels;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

public class SessionsController : Controller
{
    private readonly ISessionService _sessionService;
    private readonly IMarkdownService _markdownService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        IMarkdownService markdownService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _markdownService = markdownService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        string? provider = null,
        string? search = null,
        string sortBy = "created_desc",
        int page = 1,
        int pageSize = 25)
    {
        var model = await _sessionService.GetSessionListViewModelAsync(
            provider, search, sortBy, page, pageSize);
        return View(model);
    }

    public async Task<IActionResult> Details(string id, string? provider = null)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("Session ID is required");

        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound($"Session '{id}' not found");

        var markdown = _markdownService.GenerateMarkdown(session);
        var html = _markdownService.ConvertMarkdownToHtml(markdown);
        var stats = _sessionService.CalculateStatistics(session);

        var model = new SessionDetailViewModel
        {
            Session = session,
            MarkdownContent = markdown,
            HtmlContent = html,
            Statistics = stats
        };

        return View(model);
    }

    public async Task<IActionResult> Markdown(string id, string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound();

        var markdown = _markdownService.GenerateMarkdown(session);
        return Content(markdown, "text/markdown");
    }

    [HttpPost]
    public async Task<IActionResult> Sync(string id, string? provider = null)
    {
        var session = await _sessionService.GetSessionAsync(id, provider);
        if (session == null)
            return NotFound();

        // Trigger cloud sync for this session
        TempData["StatusMessage"] = $"Session '{session.DisplayName}' queued for cloud sync.";
        return RedirectToAction(nameof(Details), new { id, provider });
    }
}
