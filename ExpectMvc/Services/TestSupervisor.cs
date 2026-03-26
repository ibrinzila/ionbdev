using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    public string? GetLastSessionId() => _agent.GetLastSessionId();

    public async Task<TestPlan> ScanAndPlanAsync(RunTestViewModel input)
    {
        var target = Enum.TryParse<TestTarget>(input.Target, true, out var t) ? t : TestTarget.Changes;
        var provider = Enum.TryParse<AgentProvider>(input.Agent, true, out var a) ? a : AgentProvider.Claude;

        // Stage 1: Scan changes
        _logger.LogInformation("Stage 1: Scanning changes");
        var diff = await _git.ScanChangesAsync(input.RepositoryPath, target);

        // Stage 2: Generate plan (with tool execution loop)
        _logger.LogInformation("Stage 2: Generating test plan (agent will explore codebase)");
        var plan = await _agent.GeneratePlanAsync(diff, provider, input.Message, input.SessionId);
        plan.Target = target;

        return plan;
    }

    public async IAsyncEnumerable<AgentStreamEvent> ScanAndPlanStreamAsync(
        RunTestViewModel input, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var target = Enum.TryParse<TestTarget>(input.Target, true, out var t) ? t : TestTarget.Changes;
        var provider = Enum.TryParse<AgentProvider>(input.Agent, true, out var a) ? a : AgentProvider.Claude;

        // Stage 1: Scan changes
        _logger.LogInformation("Stage 1 (stream): Scanning changes");
        var diff = await _git.ScanChangesAsync(input.RepositoryPath, target);

        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Text,
            Data = $"Scanned {diff.Files.Count} changed file(s) on branch {diff.Branch}"
        };

        // Stage 2: Stream plan generation with tool calls
        _logger.LogInformation("Stage 2 (stream): Streaming plan generation");
        await foreach (var evt in _agent.GeneratePlanStreamAsync(diff, provider, input.Message, input.SessionId).WithCancellation(ct))
        {
            yield return evt;
        }
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
                        var refId = await ResolveRefIdAsync(session, step.Selector);
                        var snapshot = await _browser.ActAsync(session, refId, step.Action, step.Value);
                        stepResult.SnapshotTree = snapshot.Tree;
                    }
                    else
                    {
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

    private async Task<string> ResolveRefIdAsync(BrowserSession session, string selector)
    {
        var snapshot = await _browser.SnapshotAsync(session);

        var parts = selector.Split(':', 2);
        var targetRole = parts[0].Trim().ToLowerInvariant();
        var targetName = parts.Length > 1 ? parts[1].Trim() : "";

        string? bestRef = null;
        var bestScore = -1;

        foreach (var (refId, entry) in snapshot.Refs)
        {
            if (!entry.Role.Equals(targetRole, StringComparison.OrdinalIgnoreCase))
                continue;

            var score = 0;
            if (string.IsNullOrEmpty(targetName))
            {
                score = 1;
            }
            else if (entry.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                score = 100;
            }
            else if (entry.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                score = 50;
            }
            else if (targetName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
            {
                score = 25;
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
