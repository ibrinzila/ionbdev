namespace ExpectMvc.Models;

public class RunTestViewModel
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Message { get; set; }
    public string Agent { get; set; } = "claude";
    public string Target { get; set; } = "changes";
    public bool SkipReview { get; set; }
    public string? SessionId { get; set; }
}

public class PlanReviewViewModel
{
    public TestPlan Plan { get; set; } = new();
    public string? Url { get; set; }
    public string? SessionId { get; set; }
}

public class ResultsViewModel
{
    public TestResult Result { get; set; } = new();
    public TestPlan Plan { get; set; } = new();
    public string? SessionId { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
