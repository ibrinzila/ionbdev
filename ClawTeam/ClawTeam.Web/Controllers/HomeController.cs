using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Home page showing overview of all teams.</summary>
public class HomeController : Controller
{
    private readonly BoardCollector _collector;

    public HomeController(BoardCollector collector)
    {
        _collector = collector;
    }

    public IActionResult Index()
    {
        var teams = _collector.CollectOverview();
        return View(teams);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
