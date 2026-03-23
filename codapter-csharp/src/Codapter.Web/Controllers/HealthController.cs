using Microsoft.AspNetCore.Mvc;

namespace Codapter.Web.Controllers;

/// <summary>
/// Health check endpoints.
/// Port of the /healthz and /readyz endpoints from the TypeScript CLI.
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/healthz")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpGet("/readyz")]
    public IActionResult Ready() => Ok(new { status = "ready" });
}
