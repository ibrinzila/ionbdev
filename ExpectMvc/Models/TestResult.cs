namespace ExpectMvc.Models;

/// <summary>
/// Result of executing a test plan in the browser.
/// </summary>
public class TestResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PlanId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Passed { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? VideoPath { get; set; }
}

public class StepResult
{
    public int StepOrder { get; set; }
    public string Action { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? ActualResult { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SnapshotTree { get; set; }
    public TimeSpan Duration { get; set; }
}
