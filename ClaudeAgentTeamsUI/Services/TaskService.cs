using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class TaskService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<TaskService> _logger;
    private ConcurrentDictionary<string, TeamTask> _tasks = new();
    private bool _loaded;

    public TaskService(JsonDataStore store, ILogger<TaskService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var tasks = await _store.LoadAsync<List<TeamTask>>("tasks.json");
        _tasks = new ConcurrentDictionary<string, TeamTask>(tasks.ToDictionary(t => t.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("tasks.json", _tasks.Values.ToList());
    }

    public async Task<List<TeamTask>> GetByTeamAsync(string teamId)
    {
        await EnsureLoadedAsync();
        return _tasks.Values
            .Where(t => t.TeamId == teamId && t.Status != TaskState.Deleted)
            .OrderBy(t => t.KanbanOrder)
            .ToList();
    }

    public async Task<TeamTask?> GetByIdAsync(string id)
    {
        await EnsureLoadedAsync();
        return _tasks.GetValueOrDefault(id);
    }

    public async Task<TeamTask> CreateAsync(string teamId, string title, string? description = null,
        string? assigneeId = null, string? assigneeName = null, string? createdBy = null)
    {
        await EnsureLoadedAsync();
        var task = new TeamTask
        {
            TeamId = teamId,
            Title = title,
            Description = description,
            AssigneeId = assigneeId,
            AssigneeName = assigneeName,
            CreatedBy = createdBy,
            History = new List<TaskHistoryEvent>
            {
                new()
                {
                    EventType = "created",
                    Actor = createdBy,
                    Details = $"Task created: {title}"
                }
            }
        };
        _tasks[task.Id] = task;
        await PersistAsync();
        _logger.LogInformation("Created task {TaskTitle} for team {TeamId}", title, teamId);
        return task;
    }

    public async Task<TeamTask?> UpdateStatusAsync(string id, TaskState newStatus, string? actor = null)
    {
        await EnsureLoadedAsync();
        if (!_tasks.TryGetValue(id, out var task)) return null;

        var oldStatus = task.Status;
        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        if (newStatus == TaskState.Completed)
            task.CompletedAt = DateTime.UtcNow;

        task.History.Add(new TaskHistoryEvent
        {
            EventType = "status_change",
            Actor = actor,
            Details = $"Status changed from {oldStatus} to {newStatus}"
        });

        await PersistAsync();
        return task;
    }

    public async Task<TeamTask?> UpdateReviewStatusAsync(string id, ReviewState reviewState, string? actor = null)
    {
        await EnsureLoadedAsync();
        if (!_tasks.TryGetValue(id, out var task)) return null;

        task.ReviewStatus = reviewState;
        task.UpdatedAt = DateTime.UtcNow;
        task.History.Add(new TaskHistoryEvent
        {
            EventType = "review_change",
            Actor = actor,
            Details = $"Review status changed to {reviewState}"
        });

        await PersistAsync();
        return task;
    }

    public async Task<TeamTask?> UpdateAsync(string id, Action<TeamTask> update)
    {
        await EnsureLoadedAsync();
        if (!_tasks.TryGetValue(id, out var task)) return null;
        update(task);
        task.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
        return task;
    }

    public async Task<bool> MoveAsync(string id, TaskState targetColumn, int order)
    {
        await EnsureLoadedAsync();
        if (!_tasks.TryGetValue(id, out var task)) return false;
        task.Status = targetColumn;
        task.KanbanOrder = order;
        task.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureLoadedAsync();
        if (!_tasks.TryGetValue(id, out var task)) return false;
        task.Status = TaskState.Deleted;
        task.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
        return true;
    }

    public async Task<List<KanbanColumn>> GetKanbanColumnsAsync(string teamId)
    {
        var tasks = await GetByTeamAsync(teamId);
        return new List<KanbanColumn>
        {
            new() { Name = "Pending", State = TaskState.Pending, Tasks = tasks.Where(t => t.Status == TaskState.Pending).ToList() },
            new() { Name = "In Progress", State = TaskState.InProgress, Tasks = tasks.Where(t => t.Status == TaskState.InProgress).ToList() },
            new() { Name = "In Review", State = TaskState.InReview, Tasks = tasks.Where(t => t.Status == TaskState.InReview).ToList() },
            new() { Name = "Completed", State = TaskState.Completed, Tasks = tasks.Where(t => t.Status == TaskState.Completed).ToList() }
        };
    }

    public async Task<(int Total, int Completed, int Pending, int InProgress)> GetStatsAsync(string teamId)
    {
        var tasks = await GetByTeamAsync(teamId);
        return (
            tasks.Count,
            tasks.Count(t => t.Status == TaskState.Completed),
            tasks.Count(t => t.Status == TaskState.Pending),
            tasks.Count(t => t.Status == TaskState.InProgress)
        );
    }
}
