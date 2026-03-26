using ExpectMvc.Models;
using ExpectMvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpectMvc.Controllers;

public class TestController : Controller
{
    private readonly ITestSupervisor _supervisor;

    // In-memory store for demo; replace with a real store in production.
    private static readonly Dictionary<string, TestPlan> Plans = new();
    private static readonly Dictionary<string, TestResult> Results = new();

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

        if (model.SkipReview)
        {
            return RedirectToAction(nameof(Execute), new { planId = plan.Id, url = model.Url });
        }

        return RedirectToAction(nameof(Plan), new { id = plan.Id, url = model.Url });
    }

    [HttpGet]
    public IActionResult Plan(string id, string? url)
    {
        if (!Plans.TryGetValue(id, out var plan))
            return NotFound();

        return View(new PlanReviewViewModel { Plan = plan, Url = url });
    }

    [HttpPost]
    public async Task<IActionResult> Execute(string planId, string url)
    {
        if (!Plans.TryGetValue(planId, out var plan))
            return NotFound();

        var result = await _supervisor.ExecutePlanAsync(plan, url);
        Results[result.Id] = result;

        return RedirectToAction(nameof(ResultDetail), new { id = result.Id });
    }

    [HttpGet]
    public IActionResult ResultDetail(string id)
    {
        if (!Results.TryGetValue(id, out var result))
            return NotFound();

        Plans.TryGetValue(result.PlanId, out var plan);

        return View("Results", new ResultsViewModel
        {
            Result = result,
            Plan = plan ?? new TestPlan()
        });
    }
}
