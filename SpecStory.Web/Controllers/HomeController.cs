using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Models.ViewModels;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

public class HomeController : Controller
{
    private readonly ISessionService _sessionService;
    private readonly IProviderService _providerService;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ISessionService sessionService,
        IProviderService providerService,
        ICloudSyncService cloudSyncService,
        ILogger<HomeController> logger)
    {
        _sessionService = sessionService;
        _providerService = providerService;
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var sessions = await _sessionService.ListSessionsAsync();
        var providers = _providerService.CheckAllProviders();

        var model = new DashboardViewModel
        {
            TotalSessions = sessions.Count,
            Providers = providers,
            RecentSessions = sessions.Take(10).ToList(),
            SessionsByProvider = sessions
                .GroupBy(s => s.ProviderName ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Calculate aggregates from all sessions
        var allSessions = await _sessionService.GetAllSessionsAsync();
        model.TotalExchanges = allSessions.Sum(s => s.Exchanges.Count);
        model.TotalToolUses = allSessions.Sum(s => s.TotalToolUses);
        model.TotalTokens = allSessions.Sum(s => (long)s.TotalTokens);

        return View(model);
    }

    public IActionResult About()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
