using Water.Core;
using Water.Guardrails;
using Xunit;

namespace Water.Tests;

public class GuardrailTests
{
    [Fact]
    public void ContentFilter_BlocksForbiddenContent()
    {
        var filter = new ContentFilter(new List<string> { "password", "secret" });

        var result = filter.Validate(new Dictionary<string, object?> { ["text"] = "my password is 123" });
        Assert.False(result.Passed);

        result = filter.Validate(new Dictionary<string, object?> { ["text"] = "hello world" });
        Assert.True(result.Passed);
    }

    [Fact]
    public void ContentFilter_ThrowsOnBlock()
    {
        var filter = new ContentFilter(new List<string> { "blocked" }, action: GuardrailAction.Block);

        Assert.Throws<GuardrailViolationException>(() =>
            filter.Check(new Dictionary<string, object?> { ["content"] = "this is blocked content" }));
    }

    [Fact]
    public void CostGuardrail_BlocksOverBudget()
    {
        var guardrail = new CostGuardrail(1.0);

        var result = guardrail.Validate(new Dictionary<string, object?> { ["estimated_cost"] = 0.5 });
        Assert.True(result.Passed);

        result = guardrail.Validate(new Dictionary<string, object?> { ["estimated_cost"] = 1.5 });
        Assert.False(result.Passed);
    }

    [Fact]
    public void GuardrailChain_RunsAll()
    {
        var chain = new GuardrailChain()
            .Add(new ContentFilter(new List<string> { "bad" }, action: GuardrailAction.Warn))
            .Add(new TopicGuardrail(new List<string> { "politics" }, action: GuardrailAction.Warn));

        var results = chain.Check(new Dictionary<string, object?> { ["text"] = "good content" });
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Passed));
    }
}
