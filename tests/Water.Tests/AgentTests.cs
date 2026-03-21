using Water.Agents;
using Water.Core;
using Xunit;
using ExecutionContext = Water.Core.ExecutionContext;

namespace Water.Tests;

public class AgentTests
{
    [Fact]
    public async Task MockProvider_ReturnsCannedResponse()
    {
        var provider = new MockProvider(new[] { "Hello!", "World!" });

        var result1 = await provider.CompleteAsync(new List<Dictionary<string, string>>
        {
            new() { ["role"] = "user", ["content"] = "Hi" }
        });
        Assert.Equal("Hello!", result1["text"]);

        var result2 = await provider.CompleteAsync(new List<Dictionary<string, string>>
        {
            new() { ["role"] = "user", ["content"] = "Next" }
        });
        Assert.Equal("World!", result2["text"]);
    }

    [Fact]
    public async Task MockProvider_FallsBackToDefault()
    {
        var provider = new MockProvider(defaultResponse: "default");
        var result = await provider.CompleteAsync(new List<Dictionary<string, string>>());
        Assert.Equal("default", result["text"]);
    }

    [Fact]
    public async Task AgentTask_ExecutesWithProvider()
    {
        var provider = new MockProvider(new[] { "AI response" });
        var task = AgentTaskFactory.Create(provider, systemPrompt: "You are helpful.");

        var result = await task.Execute(
            new Dictionary<string, object?> { ["input_data"] = new Dictionary<string, object?> { ["prompt"] = "Hello" } },
            new ExecutionContext("test_flow"));

        Assert.Equal("AI response", result["response"]);
    }

    [Fact]
    public async Task MultiAgent_RunsAllAgents()
    {
        var orchestrator = new AgentOrchestrator(new[]
        {
            new AgentRole("analyst", "Analyzes data", "You analyze data.", new MockProvider(new[] { "Analysis complete" })),
            new AgentRole("writer", "Writes reports", "You write reports.", new MockProvider(new[] { "Report written" }))
        });

        var results = await orchestrator.RunRoundAsync("Process this data");

        Assert.Equal("Analysis complete", results["analyst"]);
        Assert.Equal("Report written", results["writer"]);
    }

    [Fact]
    public void Toolkit_ManagesTools()
    {
        var toolkit = new Toolkit();
        toolkit.Add(new Tool("search", "Search the web", new(), args => Task.FromResult(ToolResult.Ok("result"))));
        toolkit.Add(new Tool("calc", "Calculate", new(), args => Task.FromResult(ToolResult.Ok(42))));

        Assert.Equal(2, toolkit.Count);
        Assert.NotNull(toolkit.Get("search"));
        Assert.Null(toolkit.Get("nonexistent"));
    }

    [Fact]
    public async Task ToolExecutor_ExecutesTool()
    {
        var toolkit = new Toolkit(new[]
        {
            new Tool("add", "Add numbers", new(), args =>
            {
                var a = Convert.ToInt32(args["a"]);
                var b = Convert.ToInt32(args["b"]);
                return Task.FromResult(ToolResult.Ok(a + b));
            })
        });

        var executor = new ToolExecutor(toolkit);
        var result = await executor.ExecuteAsync("add", new Dictionary<string, object?> { ["a"] = 3, ["b"] = 4 });

        Assert.True(result.Success);
        Assert.Equal(7, result.Output);
    }
}
