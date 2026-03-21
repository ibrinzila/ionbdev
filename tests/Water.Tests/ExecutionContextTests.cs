using Water.Core;
using Xunit;
using ExecutionContext = Water.Core.ExecutionContext;

namespace Water.Tests;

public class ExecutionContextTests
{
    [Fact]
    public void Context_StoresTaskOutputs()
    {
        var ctx = new ExecutionContext("flow1");

        ctx.AddTaskOutput("task1", new Dictionary<string, object?> { ["result"] = 42 });

        var output = ctx.GetTaskOutput("task1");
        Assert.NotNull(output);
        Assert.Equal(42, output["result"]);
    }

    [Fact]
    public void Context_ReturnsNullForMissingTask()
    {
        var ctx = new ExecutionContext("flow1");
        Assert.Null(ctx.GetTaskOutput("nonexistent"));
    }

    [Fact]
    public void Context_RegistersAndRetrievesServices()
    {
        var ctx = new ExecutionContext("flow1");
        ctx.RegisterService("config", "my-config-value");

        var config = ctx.GetService<string>("config");
        Assert.Equal("my-config-value", config);
    }

    [Fact]
    public void Context_ThrowsOnMissingService()
    {
        var ctx = new ExecutionContext("flow1");
        Assert.Throws<KeyNotFoundException>(() => { ctx.GetService("missing"); });
    }

    [Fact]
    public void Context_ThrowsOnWrongServiceType()
    {
        var ctx = new ExecutionContext("flow1");
        ctx.RegisterService("number", 42);
        Assert.Throws<InvalidCastException>(() => { ctx.GetService<string>("number"); });
    }

    [Fact]
    public void Context_HasService()
    {
        var ctx = new ExecutionContext("flow1");
        Assert.False(ctx.HasService("svc"));
        ctx.RegisterService("svc", "value");
        Assert.True(ctx.HasService("svc"));
    }

    [Fact]
    public void Context_CreatesChildWithCopiedState()
    {
        var ctx = new ExecutionContext("flow1");
        ctx.AddTaskOutput("task1", new Dictionary<string, object?> { ["v"] = 1 });

        var child = ctx.CreateChildContext("child_task");

        // Child has parent's data
        Assert.NotNull(child.GetTaskOutput("task1"));

        // Child modifications don't affect parent
        child.AddTaskOutput("task2", new Dictionary<string, object?> { ["v"] = 2 });
        Assert.Null(ctx.GetTaskOutput("task2"));
    }

    [Fact]
    public void Context_ToDict_ContainsExpectedKeys()
    {
        var ctx = new ExecutionContext("flow1", executionId: "exec1", taskId: "task1");
        var dict = ctx.ToDict();

        Assert.Equal("flow1", dict["flow_id"]);
        Assert.Equal("exec1", dict["execution_id"]);
        Assert.Equal("task1", dict["task_id"]);
    }
}
