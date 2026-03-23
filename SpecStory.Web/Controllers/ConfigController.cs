using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Models;
using SpecStory.Web.Models.ViewModels;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

public class ConfigController : Controller
{
    private readonly IConfigService _configService;

    public ConfigController(IConfigService configService)
    {
        _configService = configService;
    }

    public IActionResult Index()
    {
        var model = new ConfigurationViewModel
        {
            UserConfig = _configService.GetUserConfig(),
            ProjectConfig = _configService.GetProjectConfig(),
            EffectiveConfig = _configService.GetEffectiveConfig(),
            UserConfigPath = _configService.GetUserConfigPath(),
            ProjectConfigPath = _configService.GetProjectConfigPath()
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult SaveUser(SpecStoryConfig config)
    {
        try
        {
            _configService.SaveUserConfig(config);
            TempData["StatusMessage"] = "User configuration saved successfully.";
            TempData["IsSuccess"] = true;
        }
        catch (Exception ex)
        {
            TempData["StatusMessage"] = $"Failed to save configuration: {ex.Message}";
            TempData["IsSuccess"] = false;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult SaveProject(SpecStoryConfig config)
    {
        try
        {
            _configService.SaveProjectConfig(config);
            TempData["StatusMessage"] = "Project configuration saved successfully.";
            TempData["IsSuccess"] = true;
        }
        catch (Exception ex)
        {
            TempData["StatusMessage"] = $"Failed to save configuration: {ex.Message}";
            TempData["IsSuccess"] = false;
        }

        return RedirectToAction(nameof(Index));
    }
}
