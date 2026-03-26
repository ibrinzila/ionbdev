using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class SessionsController : Controller
{
    private readonly SessionService _sessionService;
    private readonly ProjectService _projectService;

    public SessionsController(SessionService sessionService, ProjectService projectService)
    {
        _sessionService = sessionService;
        _projectService = projectService;
    }

    public async Task<IActionResult> Index(string? projectId, int page = 1)
    {
        var sessions = await _sessionService.GetSessionListAsync(projectId, page);
        var totalCount = await _sessionService.GetTotalCountAsync(projectId);

        var vm = new SessionListViewModel
        {
            Sessions = sessions,
            ProjectFilter = projectId,
            Page = page,
            TotalCount = totalCount
        };
        return View(vm);
    }

    public async Task<IActionResult> Detail(string id)
    {
        var session = await _sessionService.GetSessionAsync(id);
        if (session == null) return NotFound();

        var waterfall = await _sessionService.GetWaterfallAsync(id);
        var vm = new SessionDetailViewModel
        {
            Session = session,
            Waterfall = waterfall
        };
        return View(vm);
    }
}

[ApiController]
[Route("api/[controller]")]
public class SessionsApiController : ControllerBase
{
    private readonly SessionService _sessionService;

    public SessionsApiController(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? projectId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var sessions = await _sessionService.GetSessionListAsync(projectId, page, pageSize);
        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var session = await _sessionService.GetSessionAsync(id);
        return session != null ? Ok(session) : NotFound();
    }

    [HttpGet("{id}/waterfall")]
    public async Task<IActionResult> GetWaterfall(string id)
    {
        var waterfall = await _sessionService.GetWaterfallAsync(id);
        return Ok(waterfall);
    }

    [HttpGet("{id}/metrics")]
    public async Task<IActionResult> GetMetrics(string id)
    {
        var session = await _sessionService.GetSessionAsync(id);
        return session != null ? Ok(session.Metrics) : NotFound();
    }
}
