using ExpectMvc.Models;
using ExpectMvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpectMvc.Controllers;

/// <summary>
/// JSON API for headless/CI usage — equivalent to `expect-cli -y`.
/// POST /api/run to scan, plan, and execute in one call.
/// </summary>
[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly ITestSupervisor _supervisor;

    public ApiController(ITestSupervisor supervisor)
    {
        _supervisor = supervisor;
    }

    [HttpPost("plan")]
    public async Task<ActionResult<TestPlan>> GeneratePlan([FromBody] RunTestViewModel input)
    {
        var plan = await _supervisor.ScanAndPlanAsync(input);
        return Ok(plan);
    }

    [HttpPost("run")]
    public async Task<ActionResult<TestResult>> Run([FromBody] RunTestViewModel input)
    {
        if (string.IsNullOrEmpty(input.Url))
            return BadRequest(new { error = "url is required" });

        var plan = await _supervisor.ScanAndPlanAsync(input);
        var result = await _supervisor.ExecutePlanAsync(plan, input.Url);

        return result.Passed ? Ok(result) : UnprocessableEntity(result);
    }
}
