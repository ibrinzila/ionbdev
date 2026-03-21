namespace Water.Resilience;

/// <summary>
/// Checkpoint backend for crash recovery during flow execution.
/// </summary>
public interface ICheckpointBackend
{
    Task SaveAsync(string flowId, string executionId, int nodeIndex, Dictionary<string, object?> data);
    Task<Dictionary<string, object?>?> LoadAsync(string flowId, string executionId);
    Task ClearAsync(string flowId, string executionId);
}

/// <summary>
/// In-memory checkpoint backend for development and testing.
/// </summary>
public class InMemoryCheckpoint : ICheckpointBackend
{
    private readonly Dictionary<string, Dictionary<string, object?>> _checkpoints = new();
    private readonly object _lock = new();

    private static string Key(string flowId, string executionId) => $"{flowId}:{executionId}";

    public Task SaveAsync(string flowId, string executionId, int nodeIndex, Dictionary<string, object?> data)
    {
        lock (_lock)
        {
            _checkpoints[Key(flowId, executionId)] = new Dictionary<string, object?>
            {
                ["node_index"] = nodeIndex,
                ["data"] = data
            };
        }
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object?>?> LoadAsync(string flowId, string executionId)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _checkpoints.TryGetValue(Key(flowId, executionId), out var cp) ? cp : null);
        }
    }

    public Task ClearAsync(string flowId, string executionId)
    {
        lock (_lock) { _checkpoints.Remove(Key(flowId, executionId)); }
        return Task.CompletedTask;
    }
}
