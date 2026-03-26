using System.Collections.Concurrent;
using System.Text.Json;
using ExpectMvc.Models;
using ExpectMvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpectMvc.Controllers;

public class TestController : Controller
{
    private readonly ITestSupervisor _supervisor;

    private static readonly ConcurrentDictionary<string, TestPlan> Plans = new();
    private static readonly ConcurrentDictionary<string, TestResult> Results = new();

    public TestController(ITestSupervisor supervisor)
    {
        _supervisor = supervisor;
    }

    [HttpGet]
    public IActionResult Run()
    {
        return View(new RunTestViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Run(RunTestViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var plan = await _supervisor.ScanAndPlanAsync(model);
        Plans[plan.Id] = plan;

        var sessionId = _supervisor.GetLastSessionId();

        if (model.SkipReview)
        {
            return RedirectToAction(nameof(Execute), new { planId = plan.Id, url = model.Url });
        }

        return RedirectToAction(nameof(Plan), new { id = plan.Id, url = model.Url, sessionId });
    }

    [HttpGet]
    public IActionResult Plan(string id, string? url, string? sessionId)
    {
        if (!Plans.TryGetValue(id, out var plan))
            return NotFound();

        return View(new PlanReviewViewModel { Plan = plan, Url = url, SessionId = sessionId });
    }

    [HttpPost]
    public async Task<IActionResult> Execute(string planId, string url)
    {
        if (!Plans.TryGetValue(planId, out var plan))
            return NotFound();

        var result = await _supervisor.ExecutePlanAsync(plan, url);
        Results[result.Id] = result;

        var sessionId = _supervisor.GetLastSessionId();

        return RedirectToAction(nameof(ResultDetail), new { id = result.Id, sessionId });
    }

    /// <summary>
    /// SSE endpoint for streaming plan generation to the browser.
    /// The Run.cshtml view connects to this via EventSource.
    /// </summary>
    [HttpPost]
    public async Task Stream([FromBody] RunTestViewModel model, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var evt in _supervisor.ScanAndPlanStreamAsync(model).WithCancellation(ct))
        {
            var data = JsonSerializer.Serialize(new { type = evt.Type.ToString(), data = evt.Data });
            await Response.WriteAsync($"data: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        var sessionId = _supervisor.GetLastSessionId();
        var final = JsonSerializer.Serialize(new { type = "done", sessionId = sessionId ?? "" });
        await Response.WriteAsync($"data: {final}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpGet]
    public IActionResult ResultDetail(string id, string? sessionId)
    {
        if (!Results.TryGetValue(id, out var result))
            return NotFound();

        Plans.TryGetValue(result.PlanId, out var plan);

        return View("Results", new ResultsViewModel
        {
            Result = result,
            Plan = plan ?? new TestPlan(),
            SessionId = sessionId
        });
    }
}
