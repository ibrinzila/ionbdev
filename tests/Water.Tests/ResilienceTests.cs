using Water.Core;
using Water.Resilience;
using Xunit;

namespace Water.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public void InitialState_IsClosed()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        Assert.Equal("closed", cb.State);
        Assert.True(cb.CanExecute());
    }

    [Fact]
    public void OpensAfterThreshold()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.True(cb.CanExecute()); // Still closed

        cb.RecordFailure(); // Hits threshold
        Assert.Equal("open", cb.State);
        Assert.False(cb.CanExecute());
    }

    [Fact]
    public void ResetsOnSuccess()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess();

        Assert.Equal("closed", cb.State);
        Assert.True(cb.CanExecute());
    }
}

public class TaskCacheTests
{
    [Fact]
    public void SetAndGet_ReturnsValue()
    {
        var cache = new InMemoryCache();
        cache.Set("key1", "value1");
        Assert.Equal("value1", cache.Get("key1"));
    }

    [Fact]
    public void Get_ReturnsNullForMissing()
    {
        var cache = new InMemoryCache();
        Assert.Null(cache.Get("nonexistent"));
    }

    [Fact]
    public void Has_ReturnsCorrectly()
    {
        var cache = new InMemoryCache();
        Assert.False(cache.Has("key1"));
        cache.Set("key1", "value1");
        Assert.True(cache.Has("key1"));
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var cache = new InMemoryCache();
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Clear();
        Assert.Null(cache.Get("key1"));
        Assert.Null(cache.Get("key2"));
    }
}

public class DeadLetterQueueTests
{
    [Fact]
    public async Task Enqueue_And_GetAll()
    {
        var dlq = new InMemoryDlq();
        await dlq.EnqueueAsync(new DeadLetter("task1", new() { ["k"] = "v" }, "error1"));
        await dlq.EnqueueAsync(new DeadLetter("task2", new(), "error2"));

        var letters = await dlq.GetAllAsync();
        Assert.Equal(2, letters.Count);
        Assert.Equal(2, await dlq.CountAsync());
    }

    [Fact]
    public async Task Clear_RemovesAll()
    {
        var dlq = new InMemoryDlq();
        await dlq.EnqueueAsync(new DeadLetter("task1", new(), "error1"));
        await dlq.ClearAsync();
        Assert.Equal(0, await dlq.CountAsync());
    }
}
