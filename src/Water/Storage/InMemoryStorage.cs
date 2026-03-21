namespace Water.Storage;

/// <summary>
/// In-memory storage backend for development and testing.
/// </summary>
public class InMemoryStorage : IStorageBackend
{
    private readonly Dictionary<string, FlowSession> _sessions = new();
    private readonly Dictionary<string, List<TaskRun>> _taskRuns = new();
    private readonly object _lock = new();

    public Task SaveSessionAsync(FlowSession session)
    {
        lock (_lock)
        {
            session.UpdatedAt = DateTime.UtcNow;
            _sessions[session.ExecutionId] = session;
        }
        return Task.CompletedTask;
    }

    public Task<FlowSession?> GetSessionAsync(string executionId)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.TryGetValue(executionId, out var session) ? session : null);
        }
    }

    public Task<List<FlowSession>> ListSessionsAsync(string? flowId = null)
    {
        lock (_lock)
        {
            var sessions = _sessions.Values.AsEnumerable();
            if (flowId != null)
                sessions = sessions.Where(s => s.FlowId == flowId);
            return Task.FromResult(sessions.ToList());
        }
    }

    public Task SaveTaskRunAsync(TaskRun taskRun)
    {
        lock (_lock)
        {
            if (!_taskRuns.TryGetValue(taskRun.ExecutionId, out var runs))
            {
                runs = new List<TaskRun>();
                _taskRuns[taskRun.ExecutionId] = runs;
            }

            var existing = runs.FindIndex(r => r.Id == taskRun.Id);
            if (existing >= 0)
                runs[existing] = taskRun;
            else
                runs.Add(taskRun);
        }
        return Task.CompletedTask;
    }

    public Task<List<TaskRun>> GetTaskRunsAsync(string executionId)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _taskRuns.TryGetValue(executionId, out var runs) ? new List<TaskRun>(runs) : new List<TaskRun>());
        }
    }
}
