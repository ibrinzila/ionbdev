using Water.Storage;
using Xunit;

namespace Water.Tests;

public class InMemoryStorageTests
{
    [Fact]
    public async Task SaveAndGetSession()
    {
        var storage = new InMemoryStorage();
        var session = new FlowSession("flow1", new Dictionary<string, object?> { ["key"] = "value" });

        await storage.SaveSessionAsync(session);
        var retrieved = await storage.GetSessionAsync(session.ExecutionId);

        Assert.NotNull(retrieved);
        Assert.Equal("flow1", retrieved!.FlowId);
        Assert.Equal(FlowStatus.Pending, retrieved.Status);
    }

    [Fact]
    public async Task ListSessions_FiltersByFlowId()
    {
        var storage = new InMemoryStorage();
        await storage.SaveSessionAsync(new FlowSession("flow1", new()));
        await storage.SaveSessionAsync(new FlowSession("flow1", new()));
        await storage.SaveSessionAsync(new FlowSession("flow2", new()));

        var all = await storage.ListSessionsAsync();
        Assert.Equal(3, all.Count);

        var flow1Only = await storage.ListSessionsAsync("flow1");
        Assert.Equal(2, flow1Only.Count);
    }

    [Fact]
    public async Task SaveAndGetTaskRuns()
    {
        var storage = new InMemoryStorage();
        var run = new TaskRun("exec1", "task1", 0, FlowStatus.Running);

        await storage.SaveTaskRunAsync(run);
        var runs = await storage.GetTaskRunsAsync("exec1");

        Assert.Single(runs);
        Assert.Equal("task1", runs[0].TaskId);
    }

    [Fact]
    public async Task GetSession_ReturnsNullForMissing()
    {
        var storage = new InMemoryStorage();
        var session = await storage.GetSessionAsync("nonexistent");
        Assert.Null(session);
    }
}
