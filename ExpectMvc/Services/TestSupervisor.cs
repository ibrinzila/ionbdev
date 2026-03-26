using System.Diagnostics;
using ExpectMvc.Models;

namespace ExpectMvc.Services;

public class TestSupervisor : ITestSupervisor
{
    private readonly IGitService _git;
    private readonly IAgentService _agent;
    private readonly IBrowserService _browser;
    private readonly ILogger<TestSupervisor> _logger;

    public TestSupervisor(
        IGitService git,
        IAgentService agent,
        IBrowserService browser,
        ILogger<TestSupervisor> logger)
    {
        _git = git;
        _agent = agent;
        _browser = browser;
        _logger = logger;
    }

    public async Task<TestPlan> ScanAndPlanAsync(RunTestViewModel input)
    {
        var target = Enum.TryParse<TestTarget>(input.Target, true, out var t) ? t : TestTarget.Changes;
        var provider = Enum.TryParse<AgentProvider>(input.Agent, true, out var a) ? a : AgentProvider.Claude;

        // Stage 1: Scan changes
        _logger.LogInformation("Stage 1: Scanning changes");
        var diff = await _git.ScanChangesAsync(input.RepositoryPath, target);

        // Stage 2: Generate plan
        _logger.LogInformation("Stage 2: Generating test plan");
        var plan = await _agent.GeneratePlanAsync(diff, provider, input.Message);

        return plan;
    }

    public async Task<TestResult> ExecutePlanAsync(TestPlan plan, string url)
    {
        var result = new TestResult
        {
            PlanId = plan.Id,
            StartedAt = DateTime.UtcNow
        };

        BrowserSession? session = null;

        try
        {
            // Stage 3: Run in browser
            _logger.LogInformation("Stage 3: Executing {Count} steps in browser", plan.Steps.Count);
            session = await _browser.CreatePageAsync(url, new BrowserOptions
            {
                Headed = false,
                RecordVideo = true
            });

            foreach (var step in plan.Steps.OrderBy(s => s.Order))
            {
                var stepResult = new StepResult
                {
                    StepOrder = step.Order,
                    Action = step.Action
                };

                var sw = Stopwatch.StartNew();

                try
                {
                    if (!string.IsNullOrEmpty(step.Selector))
                    {
                        var snapshot = await _browser.ActAsync(session, step.Selector, step.Action, step.Value);
                        stepResult.SnapshotTree = snapshot.Tree;
                    }
                    else
                    {
                        // Navigation or wait step
                        await session.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
                        var snapshot = await _browser.SnapshotAsync(session);
                        stepResult.SnapshotTree = snapshot.Tree;
                    }

                    stepResult.Passed = true;
                    stepResult.ActualResult = "Step completed successfully";
                }
                catch (Exception ex)
                {
                    stepResult.Passed = false;
                    stepResult.ErrorMessage = ex.Message;
                    _logger.LogWarning(ex, "Step {Order} failed: {Action}", step.Order, step.Action);
                }

                sw.Stop();
                stepResult.Duration = sw.Elapsed;
                result.StepResults.Add(stepResult);
            }

            result.VideoPath = session.VideoPath;

            // Stage 4: Report
            result.Passed = result.StepResults.All(s => s.Passed);
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Stage 4: Test {Result} — {Passed}/{Total} steps passed",
                result.Passed ? "PASSED" : "FAILED",
                result.StepResults.Count(s => s.Passed),
                result.StepResults.Count);
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Test execution failed");
        }
        finally
        {
            if (session != null)
                await _browser.CloseAsync(session);
        }

        return result;
    }
}
