using Microsoft.AspNetCore.Mvc;

namespace Water.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly FlowRegistry _registry;

    public HealthController(FlowRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            flows_count = _registry.Count,
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }
}
