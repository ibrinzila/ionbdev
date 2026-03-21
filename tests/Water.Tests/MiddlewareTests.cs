using Water.Core;
using Water.Middleware;
using Xunit;
using ExecutionContext = Water.Core.ExecutionContext;
using TaskFactory = Water.Core.TaskFactory;

namespace Water.Tests;

public class MiddlewareTests
{
    private static Dictionary<string, object?> ExtractData(Dictionary<string, object?> input)
    {
        return input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> data
            ? data : input;
    }

    [Fact]
    public async Task TransformMiddleware_TransformsData()
    {
        Func<string, Dictionary<string, object?>, ExecutionContext, Task<Dictionary<string, object?>>> beforeFn =
            (taskId, data, ctx) =>
            {
                data["added_by_middleware"] = true;
                return Task.FromResult(data);
            };

        var middleware = new TransformMiddleware(beforeFn: beforeFn);

        var task = TaskFactory.CreateSync(
            (input, ctx) =>
            {
                var data = ExtractData(input);
                return new Dictionary<string, object?>
                {
                    ["has_middleware_field"] = data.ContainsKey("added_by_middleware")
                };
            },
            id: "mw_test_task");

        var flow = new Flow(id: "test_middleware")
            .Use(middleware)
            .Then(task);

        var result = await flow.RunAsync(new Dictionary<string, object?> { ["original"] = "data" });
        Assert.Equal(true, result["has_middleware_field"]);
    }

    [Fact]
    public async Task HookManager_FiresCallbacks()
    {
        var hooks = new HookManager();
        var called = false;

        hooks.On("on_flow_start", (args) => { called = true; });

        await hooks.EmitAsync("on_flow_start", new Dictionary<string, object?> { ["flow_id"] = "test" });

        Assert.True(called);
    }

    [Fact]
    public void HookManager_ThrowsOnInvalidEvent()
    {
        var hooks = new HookManager();
        Assert.Throws<ArgumentException>(() => hooks.On("invalid_event", (args) => { }));
    }

    [Fact]
    public async Task EventEmitter_DeliversEvents()
    {
        var emitter = new EventEmitter();
        var sub = emitter.Subscribe();

        var evt = new FlowEvent("test_event", "flow1", "task1");
        await emitter.EmitAsync(evt);

        var received = await sub.GetAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(received);
        Assert.Equal("test_event", received!.EventType);
        Assert.Equal("flow1", received.FlowId);
    }
}
