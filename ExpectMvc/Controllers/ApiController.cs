using System.Text.Json;
using ExpectMvc.Models;
using ExpectMvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpectMvc.Controllers;

/// <summary>
/// JSON API for headless/CI usage — equivalent to `expect-cli -y`.
/// Supports streaming via SSE and session resumption.
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

    /// <summary>
    /// Generate a test plan (non-streaming). Agent will use tools to explore the codebase.
    /// Pass sessionId to resume a previous session.
    /// </summary>
    [HttpPost("plan")]
    public async Task<ActionResult<TestPlan>> GeneratePlan([FromBody] RunTestViewModel input)
    {
        var plan = await _supervisor.ScanAndPlanAsync(input);
        var sessionId = _supervisor.GetLastSessionId();

        return Ok(new { plan, sessionId });
    }

    /// <summary>
    /// Stream plan generation via Server-Sent Events.
    /// Shows tool calls, tool results, reasoning, and the final plan in real time.
    /// </summary>
    [HttpPost("plan/stream")]
    public async Task StreamPlan([FromBody] RunTestViewModel input, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var evt in _supervisor.ScanAndPlanStreamAsync(input).WithCancellation(ct))
        {
            var data = JsonSerializer.Serialize(new { type = evt.Type.ToString(), data = evt.Data });
            await Response.WriteAsync($"data: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        // Send session ID as the final event
        var sessionId = _supervisor.GetLastSessionId();
        var final = JsonSerializer.Serialize(new { type = "session", data = sessionId ?? "" });
        await Response.WriteAsync($"data: {final}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Full pipeline: scan, plan (with tool loop), execute, report.
    /// Returns 200 on pass, 422 on failure.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<TestResult>> Run([FromBody] RunTestViewModel input)
    {
        if (string.IsNullOrEmpty(input.Url))
            return BadRequest(new { error = "url is required" });

        var plan = await _supervisor.ScanAndPlanAsync(input);
        var result = await _supervisor.ExecutePlanAsync(plan, input.Url);
        var sessionId = _supervisor.GetLastSessionId();

        var response = new { result, sessionId };
        return result.Passed ? Ok(response) : UnprocessableEntity(response);
    }

    /// <summary>
    /// Stream the full pipeline via SSE: scan, plan with tool calls, then execution step results.
    /// </summary>
    [HttpPost("run/stream")]
    public async Task RunStream([FromBody] RunTestViewModel input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.Url))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "url is required" }, ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // Phase 1: Stream plan generation
        TestPlan? plan = null;
        await foreach (var evt in _supervisor.ScanAndPlanStreamAsync(input).WithCancellation(ct))
        {
            var data = JsonSerializer.Serialize(new { phase = "plan", type = evt.Type.ToString(), data = evt.Data });
            await Response.WriteAsync($"data: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            if (evt.Type == AgentStreamEventType.PlanReady)
            {
                // Parse the plan from the final text
                plan = await _supervisor.ScanAndPlanAsync(input);
            }
        }

        if (plan == null)
        {
            var err = JsonSerializer.Serialize(new { phase = "error", type = "Error", data = "Plan generation failed" });
            await Response.WriteAsync($"data: {err}\n\n", ct);
            return;
        }

        // Phase 2: Execute and stream step results
        var result = await _supervisor.ExecutePlanAsync(plan, input.Url);
        foreach (var step in result.StepResults)
        {
            var stepData = JsonSerializer.Serialize(new
            {
                phase = "execute",
                type = "StepResult",
                data = new
                {
                    step.StepOrder,
                    step.Action,
                    step.Passed,
                    step.ActualResult,
                    step.ErrorMessage,
                    DurationMs = step.Duration.TotalMilliseconds
                }
            });
            await Response.WriteAsync($"data: {stepData}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        // Final summary
        var summary = JsonSerializer.Serialize(new
        {
            phase = "report",
            type = "Summary",
            data = new
            {
                result.Passed,
                result.VideoPath,
                SessionId = _supervisor.GetLastSessionId(),
                StepsPassed = result.StepResults.Count(s => s.Passed),
                StepsTotal = result.StepResults.Count
            }
        });
        await Response.WriteAsync($"data: {summary}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
