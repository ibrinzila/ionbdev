using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Models.ViewModels;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

public class CloudController : Controller
{
    private readonly ICloudSyncService _cloudSyncService;
    private readonly IConfigService _configService;
    private readonly ILogger<CloudController> _logger;

    public CloudController(
        ICloudSyncService cloudSyncService,
        IConfigService configService,
        ILogger<CloudController> logger)
    {
        _cloudSyncService = cloudSyncService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var config = _configService.GetEffectiveConfig();
        var model = new CloudSyncViewModel
        {
            IsAuthenticated = await _cloudSyncService.IsAuthenticatedAsync(),
            CloudUrl = config.CloudSync.CloudUrl,
            SyncEnabled = config.CloudSync.Enabled
        };

        return View(model);
    }

    public async Task<IActionResult> Login()
    {
        var loginUrl = await _cloudSyncService.GetLoginUrlAsync();
        if (loginUrl == null)
            return BadRequest("Could not generate login URL");

        var model = new CloudSyncViewModel
        {
            CloudUrl = loginUrl
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> LoginCallback(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest("Refresh token is required");

        var success = await _cloudSyncService.LoginWithRefreshTokenAsync(refreshToken);
        if (success)
        {
            TempData["StatusMessage"] = "Successfully logged in to SpecStory Cloud.";
        }
        else
        {
            TempData["StatusMessage"] = "Failed to log in.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _cloudSyncService.LogoutAsync();
        TempData["StatusMessage"] = "Successfully logged out.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SyncAll(string? provider = null)
    {
        var result = await _cloudSyncService.SyncAllSessionsAsync(provider);

        var model = new CloudSyncViewModel
        {
            IsAuthenticated = await _cloudSyncService.IsAuthenticatedAsync(),
            CloudUrl = _configService.GetEffectiveConfig().CloudSync.CloudUrl,
            SyncEnabled = true,
            LastSyncResult = result,
            StatusMessage = result.HasErrors
                ? $"Sync completed with {result.Errored} error(s). Created: {result.Created}, Updated: {result.Updated}"
                : $"Sync completed. Created: {result.Created}, Updated: {result.Updated}, Skipped: {result.Skipped}"
        };

        return View(nameof(Index), model);
    }
}
