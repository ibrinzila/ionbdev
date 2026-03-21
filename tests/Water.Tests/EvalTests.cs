using Water.Eval;
using Xunit;

namespace Water.Tests;

public class EvalTests
{
    [Fact]
    public void ExactMatch_MatchesCorrectly()
    {
        var evaluator = new ExactMatch("response");

        var result = evaluator.Evaluate(
            new() { ["response"] = "hello" },
            new() { ["response"] = "hello" });
        Assert.True(result.Passed);
        Assert.Equal(1.0, result.Score);

        result = evaluator.Evaluate(
            new() { ["response"] = "hello" },
            new() { ["response"] = "world" });
        Assert.False(result.Passed);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public void ContainsMatch_MatchesSubstring()
    {
        var evaluator = new ContainsMatch("response");

        var result = evaluator.Evaluate(
            new() { ["response"] = "hello world" },
            new() { ["response"] = "world" });
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task EvalSuite_RunsAllCases()
    {
        var suite = new EvalSuite("test_suite")
            .AddCase(new EvalCase("case1",
                new() { ["prompt"] = "hello" },
                new() { ["response"] = "hello back" }))
            .AddCase(new EvalCase("case2",
                new() { ["prompt"] = "bye" },
                new() { ["response"] = "goodbye" }))
            .AddEvaluator(new ExactMatch("response"));

        var report = await suite.RunAsync(async input =>
        {
            var prompt = input.TryGetValue("prompt", out var p) ? p?.ToString() : "";
            return new Dictionary<string, object?> { ["response"] = prompt == "hello" ? "hello back" : "see ya" };
        });

        Assert.Equal(2, report.TotalCases);
        Assert.Equal(1, report.Passed);
        Assert.Equal(1, report.Failed);
    }
}
