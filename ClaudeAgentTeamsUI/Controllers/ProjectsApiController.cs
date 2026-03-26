using Microsoft.AspNetCore.Mvc;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsApiController : ControllerBase
{
    private readonly ProjectService _projectService;

    public ProjectsApiController(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectService.GetAllAsync();
        return Ok(projects);
    }

    [HttpGet("repository-groups")]
    public async Task<IActionResult> GetRepositoryGroups()
    {
        var groups = await _projectService.GetRepositoryGroupsAsync();
        return Ok(groups);
    }
}
