using Microsoft.AspNetCore.Mvc;
using SpecStory.Web.Services;

namespace SpecStory.Web.Controllers;

public class ProvidersController : Controller
{
    private readonly IProviderService _providerService;

    public ProvidersController(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public IActionResult Index()
    {
        var results = _providerService.CheckAllProviders();
        return View(results);
    }

    public IActionResult Check(string id)
    {
        var result = _providerService.CheckProvider(id);
        return View("Details", result);
    }
}
