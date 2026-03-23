using Codapter.Core.Models;
using Codapter.Core.Services;
using Xunit;

namespace Codapter.Tests;

public class ThreadRegistryTests : IAsyncLifetime
{
    private readonly string _tempFile;
    private readonly ThreadRegistry _registry;

    public ThreadRegistryTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"codapter-test-{Guid.NewGuid()}.json");
        _registry = new ThreadRegistry(_tempFile);
    }

    public Task InitializeAsync() => _registry.LoadAsync();

    public Task DisposeAsync()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_ReturnsEntryWithGeneratedId()
    {
        var entry = await _registry.CreateAsync(new CreateThreadRegistryEntry
        {
            Title = "Test Thread",
            Model = "claude-3",
            Path = "/tmp/test"
        });

        Assert.NotNull(entry.ThreadId);
        Assert.Equal("Test Thread", entry.Title);
        Assert.Equal("claude-3", entry.Model);
        Assert.Equal(ThreadStatus.Active, entry.Status);
    }

    [Fact]
    public async Task List_ReturnsSortedByUpdateTime()
    {
        await _registry.CreateAsync(new CreateThreadRegistryEntry { Title = "First" });
        await Task.Delay(10);
        await _registry.CreateAsync(new CreateThreadRegistryEntry { Title = "Second" });

        var list = await _registry.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("Second", list[0].Title);
        Assert.Equal("First", list[1].Title);
    }

    [Fact]
    public async Task Update_ModifiesExistingEntry()
    {
        var entry = await _registry.CreateAsync(new CreateThreadRegistryEntry { Title = "Original" });

        var updated = await _registry.UpdateAsync(entry.ThreadId, new UpdateThreadRegistryEntry
        {
            Title = "Modified",
            Status = ThreadStatus.Archived
        });

        Assert.Equal("Modified", updated.Title);
        Assert.Equal(ThreadStatus.Archived, updated.Status);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        var entry = await _registry.CreateAsync(new CreateThreadRegistryEntry { Title = "ToDelete" });
        await _registry.DeleteAsync(entry.ThreadId);

        var result = await _registry.GetAsync(entry.ThreadId);
        Assert.Null(result);
    }

    [Fact]
    public async Task Persistence_SurvivesReload()
    {
        await _registry.CreateAsync(new CreateThreadRegistryEntry { Title = "Persistent" });

        var registry2 = new ThreadRegistry(_tempFile);
        await registry2.LoadAsync();

        var list = await registry2.ListAsync();
        Assert.Single(list);
        Assert.Equal("Persistent", list[0].Title);
    }
}
