using Water.Storage;

namespace Water.Observability;

/// <summary>
/// Flow dashboard for monitoring execution sessions.
/// </summary>
public class FlowDashboard
{
    private readonly IStorageBackend? _storage;

    public FlowDashboard(IStorageBackend? storage = null)
    {
        _storage = storage;
    }

    public async Task<Dictionary<string, object?>> GetStatsAsync()
    {
        if (_storage == null)
            return new Dictionary<string, object?> { ["error"] = "No storage backend configured" };

        var sessions = await _storage.ListSessionsAsync();
        return new Dictionary<string, object?>
        {
            ["total_sessions"] = sessions.Count,
            ["running"] = sessions.Count(s => s.Status == FlowStatus.Running),
            ["completed"] = sessions.Count(s => s.Status == FlowStatus.Completed),
            ["failed"] = sessions.Count(s => s.Status == FlowStatus.Failed),
            ["paused"] = sessions.Count(s => s.Status == FlowStatus.Paused)
        };
    }

    public async Task<List<Dictionary<string, object?>>> GetSessionsListAsync(string? flowId = null, int limit = 50, int offset = 0)
    {
        if (_storage == null) return new();

        var sessions = await _storage.ListSessionsAsync(flowId);
        return sessions
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(s => new Dictionary<string, object?>
            {
                ["execution_id"] = s.ExecutionId,
                ["flow_id"] = s.FlowId,
                ["status"] = s.Status.ToString().ToLower(),
                ["created_at"] = s.CreatedAt.ToString("O"),
                ["updated_at"] = s.UpdatedAt.ToString("O"),
                ["error"] = s.Error
            })
            .ToList();
    }

    public async Task<Dictionary<string, object?>?> GetSessionDetailAsync(string executionId)
    {
        if (_storage == null) return null;

        var session = await _storage.GetSessionAsync(executionId);
        if (session == null) return null;

        var taskRuns = await _storage.GetTaskRunsAsync(executionId);

        return new Dictionary<string, object?>
        {
            ["execution_id"] = session.ExecutionId,
            ["flow_id"] = session.FlowId,
            ["status"] = session.Status.ToString().ToLower(),
            ["input_data"] = session.InputData,
            ["current_data"] = session.CurrentData,
            ["result"] = session.Result,
            ["error"] = session.Error,
            ["created_at"] = session.CreatedAt.ToString("O"),
            ["updated_at"] = session.UpdatedAt.ToString("O"),
            ["task_runs"] = taskRuns.Select(tr => new Dictionary<string, object?>
            {
                ["id"] = tr.Id,
                ["task_id"] = tr.TaskId,
                ["status"] = tr.Status.ToString().ToLower(),
                ["started_at"] = tr.StartedAt?.ToString("O"),
                ["completed_at"] = tr.CompletedAt?.ToString("O"),
                ["error"] = tr.Error
            }).ToList()
        };
    }

    public string GetSpaHtml()
    {
        return """
        <!DOCTYPE html>
        <html>
        <head><title>Water Dashboard</title></head>
        <body>
            <h1>Water Flow Dashboard</h1>
            <div id="app">
                <p>Dashboard UI - connect to /api/dashboard/stats for data</p>
            </div>
        </body>
        </html>
        """;
    }
}
