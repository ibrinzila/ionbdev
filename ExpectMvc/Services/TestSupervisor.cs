using System.Diagnostics;
using System.Text.RegularExpressions;
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
        plan.Target = target;

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
                    if (step.Action.Equals("navigate", StringComparison.OrdinalIgnoreCase))
                    {
                        // Navigation step — go to a URL or wait for load
                        if (!string.IsNullOrEmpty(step.Value))
                        {
                            await session.Page.GotoAsync(step.Value);
                        }
                        await session.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle,
                            new Microsoft.Playwright.PageWaitForLoadStateOptions { Timeout = 10000 });
                        var snapshot = await _browser.SnapshotAsync(session);
                        stepResult.SnapshotTree = snapshot.Tree;
                    }
                    else if (!string.IsNullOrEmpty(step.Selector))
                    {
                        // Resolve the AI's "role:name" selector to the current snapshot's ref IDs
                        var refId = await ResolveRefIdAsync(session, step.Selector);
                        var snapshot = await _browser.ActAsync(session, refId, step.Action, step.Value);
                        stepResult.SnapshotTree = snapshot.Tree;
                    }
                    else
                    {
                        // Wait step with no selector
                        await session.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle,
                            new Microsoft.Playwright.PageWaitForLoadStateOptions { Timeout = 10000 });
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

    /// <summary>
    /// Resolves an AI-generated "role:name" descriptor to a live snapshot ref ID.
    /// The AI generates selectors like "button:Sign In" at plan time, but actual
    /// ref IDs (e1, e2...) only exist at runtime. This bridges the gap.
    /// </summary>
    private async Task<string> ResolveRefIdAsync(BrowserSession session, string selector)
    {
        var snapshot = await _browser.SnapshotAsync(session);

        // Parse "role:name" format from AI
        var parts = selector.Split(':', 2);
        var targetRole = parts[0].Trim().ToLowerInvariant();
        var targetName = parts.Length > 1 ? parts[1].Trim() : "";

        // Find the best matching ref by role and name
        string? bestRef = null;
        var bestScore = -1;

        foreach (var (refId, entry) in snapshot.Refs)
        {
            var roleMatch = entry.Role.Equals(targetRole, StringComparison.OrdinalIgnoreCase);
            if (!roleMatch) continue;

            var score = 0;
            if (string.IsNullOrEmpty(targetName))
            {
                score = 1; // Role-only match
            }
            else if (entry.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                score = 100; // Exact name match
            }
            else if (entry.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                score = 50; // Partial name match
            }
            else if (targetName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
            {
                score = 25; // Reverse partial match
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestRef = refId;
            }
        }

        if (bestRef == null)
            throw new InvalidOperationException(
                $"Could not resolve selector '{selector}' to any element in the current page. " +
                $"Available refs: {string.Join(", ", snapshot.Refs.Select(r => $"{r.Key}={r.Value.Role}:\"{r.Value.Name}\""))}");

        _logger.LogInformation("Resolved '{Selector}' to ref {RefId} ({Role} \"{Name}\")",
            selector, bestRef, snapshot.Refs[bestRef].Role, snapshot.Refs[bestRef].Name);

        return bestRef;
    }
}
