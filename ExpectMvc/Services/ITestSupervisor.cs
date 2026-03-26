using ExpectMvc.Models;

namespace ExpectMvc.Services;

/// <summary>
/// Orchestrates the full Expect pipeline: Scan → Plan → Run → Report.
/// Mirrors @expect/supervisor.
/// </summary>
public interface ITestSupervisor
{
    Task<TestPlan> ScanAndPlanAsync(RunTestViewModel input);
    Task<TestResult> ExecutePlanAsync(TestPlan plan, string url);
}
