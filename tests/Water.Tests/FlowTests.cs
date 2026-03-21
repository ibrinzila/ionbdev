using Water.Core;
using Xunit;

namespace Water.Tests;

public class FlowTests
{
    [Fact]
    public async Task SequentialFlow_ExecutesTasks()
    {
        var task1 = TaskFactory.CreateSync(
            (input, ctx) =>
            {
                var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
                var value = data.TryGetValue("value", out var v) && v is int i ? i : 0;
                return new Dictionary<string, object?> { ["value"] = value + 1 };
            },
            id: "add_one");

        var task2 = TaskFactory.CreateSync(
            (input, ctx) =>
            {
                var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
                var value = data.TryGetValue("value", out var v) && v is int i ? i : 0;
                return new Dictionary<string, object?> { ["value"] = value * 2 };
            },
            id: "double");

        var flow = new Flow(id: "test_sequential")
            .Then(task1)
            .Then(task2);

        var result = await flow.RunAsync(new Dictionary<string, object?> { ["value"] = 5 });

        Assert.Equal(12, result["value"]);
    }

    [Fact]
    public async Task ParallelFlow_MergesResults()
    {
        var taskA = TaskFactory.CreateSync(
            (input, ctx) => new Dictionary<string, object?> { ["a_result"] = "hello" },
            id: "task_a");

        var taskB = TaskFactory.CreateSync(
            (input, ctx) => new Dictionary<string, object?> { ["b_result"] = "world" },
            id: "task_b");

        var flow = new Flow(id: "test_parallel")
            .Parallel(new List<WaterTask> { taskA, taskB });

        var result = await flow.RunAsync(new Dictionary<string, object?>());

        Assert.Equal("hello", result["a_result"]);
        Assert.Equal("world", result["b_result"]);
    }

    [Fact]
    public async Task BranchFlow_ExecutesMatchingBranch()
    {
        var highTask = TaskFactory.CreateSync(
            (input, ctx) => new Dictionary<string, object?> { ["tier"] = "high" },
            id: "high_task");

        var lowTask = TaskFactory.CreateSync(
            (input, ctx) => new Dictionary<string, object?> { ["tier"] = "low" },
            id: "low_task");

        var flow = new Flow(id: "test_branch")
            .Branch(new List<(Func<Dictionary<string, object?>, bool>, WaterTask)>
            {
                (data => data.TryGetValue("score", out var s) && s is int score && score > 50, highTask),
                (data => true, lowTask)
            });

        var result = await flow.RunAsync(new Dictionary<string, object?> { ["score"] = 75 });
        Assert.Equal("high", result["tier"]);

        result = await flow.RunAsync(new Dictionary<string, object?> { ["score"] = 25 });
        Assert.Equal("low", result["tier"]);
    }

    [Fact]
    public async Task LoopFlow_IteratesUntilConditionFalse()
    {
        var incrementTask = TaskFactory.CreateSync(
            (input, ctx) =>
            {
                var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
                var count = data.TryGetValue("count", out var c) && c is int i ? i : 0;
                return new Dictionary<string, object?> { ["count"] = count + 1 };
            },
            id: "increment");

        var flow = new Flow(id: "test_loop")
            .Loop(
                condition: data => data.TryGetValue("count", out var c) && c is int count && count < 5,
                task: incrementTask,
                maxIterations: 100);

        var result = await flow.RunAsync(new Dictionary<string, object?> { ["count"] = 0 });
        Assert.Equal(5, result["count"]);
    }

    [Fact]
    public async Task TryCatchFlow_ExecutesFallback()
    {
        var failingTask = new WaterTask(
            execute: (input, ctx) => throw new Exception("Intentional failure"),
            id: "failing_task");

        var catchTask = TaskFactory.CreateSync(
            (input, ctx) =>
            {
                var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
                return new Dictionary<string, object?>
                {
                    ["recovered"] = true,
                    ["error"] = data.TryGetValue("_error", out var e) ? e : null
                };
            },
            id: "catch_task");

        var flow = new Flow(id: "test_try_catch")
            .TryCatch(failingTask, catchTask);

        var result = await flow.RunAsync(new Dictionary<string, object?>());
        Assert.Equal(true, result["recovered"]);
        Assert.Equal("Intentional failure", result["error"]);
    }

    [Fact]
    public async Task ConditionalThen_SkipsWhenFalse()
    {
        var task = TaskFactory.CreateSync(
            (input, ctx) => new Dictionary<string, object?> { ["executed"] = true },
            id: "conditional_task");

        var flow = new Flow(id: "test_conditional")
            .Then(task, when: data => data.TryGetValue("should_run", out var s) && s is bool b && b);

        var result = await flow.RunAsync(new Dictionary<string, object?> { ["should_run"] = false, ["original"] = "data" });
        Assert.False(result.ContainsKey("executed"));
        Assert.Equal("data", result["original"]);
    }

    [Fact]
    public async Task DependencyInjection_ServicesAvailable()
    {
        var task = new WaterTask(
            execute: (input, ctx) =>
            {
                var greeting = ctx.GetService<string>("greeting");
                return Task.FromResult<Dictionary<string, object?>>(new() { ["message"] = greeting });
            },
            id: "di_task");

        var flow = new Flow(id: "test_di")
            .Inject("greeting", "Hello, Water!")
            .Then(task);

        var result = await flow.RunAsync(new Dictionary<string, object?>());
        Assert.Equal("Hello, Water!", result["message"]);
    }
}
