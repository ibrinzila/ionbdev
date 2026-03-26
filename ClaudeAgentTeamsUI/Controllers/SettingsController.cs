using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class SettingsController : Controller
{
    private readonly ConfigService _configService;

    public SettingsController(ConfigService configService)
    {
        _configService = configService;
    }

    public async Task<IActionResult> Index()
    {
        var config = await _configService.GetAsync();
        var vm = new SettingsViewModel
        {
            Config = config,
            CliVersion = "1.0.0",
            CliStatus = "Available"
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AppConfig config)
    {
        await _configService.SaveAsync(config);
        TempData["Success"] = "Settings saved successfully";
        return RedirectToAction(nameof(Index));
    }
}
