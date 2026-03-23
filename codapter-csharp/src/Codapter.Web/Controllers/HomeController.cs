using Microsoft.AspNetCore.Mvc;

namespace Codapter.Web.Controllers;

/// <summary>
/// Home controller for the MVC dashboard.
/// </summary>
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Threads()
    {
        return View();
    }

    public IActionResult Config()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
