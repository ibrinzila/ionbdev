using Water.Core;

namespace Water.Utils;

/// <summary>
/// Mock task for testing that returns canned data.
/// </summary>
public class MockTask
{
    public static WaterTask Create(
        Dictionary<string, object?> returnData,
        string? id = null,
        string? description = null,
        int callDelay = 0)
    {
        var callCount = 0;

        return new WaterTask(
            execute: async (parameters, context) =>
            {
                Interlocked.Increment(ref callCount);
                if (callDelay > 0) await Task.Delay(callDelay);
                return new Dictionary<string, object?>(returnData);
            },
            id: id ?? $"mock_{Guid.NewGuid().ToString("N")[..8]}",
            description: description ?? "Mock task");
    }

    public static WaterTask CreateFailing(
        string errorMessage = "Mock failure",
        string? id = null)
    {
        return new WaterTask(
            execute: (parameters, context) => throw new WaterException(errorMessage),
            id: id ?? $"mock_fail_{Guid.NewGuid().ToString("N")[..8]}",
            description: "Failing mock task");
    }
}

/// <summary>
/// Test runner that simplifies flow testing.
/// </summary>
public class FlowTestRunner
{
    private readonly Flow _flow;

    public FlowTestRunner(Flow flow)
    {
        _flow = flow;
    }

    public async Task<Dictionary<string, object?>> RunAsync(Dictionary<string, object?> input)
    {
        return await _flow.RunAsync(input);
    }

    public async Task<(Dictionary<string, object?> result, TimeSpan duration)> RunTimedAsync(Dictionary<string, object?> input)
    {
        var start = DateTime.UtcNow;
        var result = await _flow.RunAsync(input);
        var duration = DateTime.UtcNow - start;
        return (result, duration);
    }
}

/// <summary>
/// Secrets manager for securely accessing environment variables.
/// </summary>
public class SecretsManager
{
    private readonly Dictionary<string, string> _overrides = new();

    public SecretsManager Set(string key, string value)
    {
        _overrides[key] = value;
        return this;
    }

    public string? Get(string key)
    {
        if (_overrides.TryGetValue(key, out var value))
            return value;
        return Environment.GetEnvironmentVariable(key);
    }

    public string GetRequired(string key)
    {
        return Get(key) ?? throw new WaterException($"Required secret '{key}' not found");
    }
}
