using System.Text.Json;
using ClawTeam.Web.Models;

namespace ClawTeam.Web.Services;

/// <summary>
/// File-based task management. Each task is stored as
/// {DataDir}/tasks/{Team}/task-{id}.json with file locking for concurrency.
/// </summary>
public class TaskStore
{
    private readonly string _teamName;
    private readonly string _dataDir;
    private readonly object _fileLock = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TaskStore(string dataDir, string teamName)
    {
        _dataDir = dataDir;
        _teamName = teamName;
    }

    private string TasksDir()
    {
        var dir = Path.Combine(_dataDir, "tasks", _teamName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string TaskPath(string taskId) =>
        Path.Combine(TasksDir(), $"task-{taskId}.json");

    // ── Persistence ──────────────────────────────────────────────────

    private TaskItem? LoadTask(string taskId)
    {
        var path = TaskPath(taskId);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
        }
        catch { return null; }
    }

    private void SaveTask(TaskItem task)
    {
        var path = TaskPath(task.Id);
        var json = JsonSerializer.Serialize(task, JsonOpts);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    // ── CRUD ─────────────────────────────────────────────────────────

    public TaskItem Create(string subject, string description = "",
        string? owner = null, string priority = "medium",
        List<string>? blockedBy = null)
    {
        var task = new TaskItem
        {
            Subject = subject,
            Description = description,
            Owner = owner,
            Priority = priority,
            BlockedBy = blockedBy ?? new(),
        };

        // Validate no circular dependencies
        if (task.BlockedBy.Count > 0)
        {
            ValidateNoCycles(task.Id, task.BlockedBy);
        }

        // Set status to blocked if there are unresolved dependencies
        if (task.BlockedBy.Count > 0 && HasUnresolvedDeps(task.BlockedBy))
        {
            task.Status = "blocked";
        }

        // Update reverse links: add this task to the "blocks" list of deps
        foreach (var depId in task.BlockedBy)
        {
            lock (_fileLock)
            {
                var dep = LoadTask(depId);
                if (dep != null && !dep.Blocks.Contains(task.Id))
                {
                    dep.Blocks.Add(task.Id);
                    SaveTask(dep);
                }
            }
        }

        SaveTask(task);
        return task;
    }

    public TaskItem? Get(string taskId) => LoadTask(taskId);

    public TaskItem Update(string taskId, string? status = null, string? owner = null,
        string? priority = null, string? lockedBy = null)
    {
        lock (_fileLock)
        {
            var task = LoadTask(taskId)
                ?? throw new InvalidOperationException($"Task '{taskId}' not found");

            if (status != null)
            {
                var oldStatus = task.Status;
                task.Status = status;

                // Track timing
                if (status == "in_progress" && oldStatus != "in_progress")
                {
                    task.StartedAt = DateTime.UtcNow.ToString("o");
                    task.LockedBy = lockedBy;
                }
                else if (status == "completed")
                {
                    task.CompletedAt = DateTime.UtcNow.ToString("o");
                    task.LockedBy = null;

                    // Calculate duration
                    if (task.StartedAt != null &&
                        DateTime.TryParse(task.StartedAt, out var started))
                    {
                        task.DurationSeconds = (DateTime.UtcNow - started).TotalSeconds;
                    }

                    // Unblock dependent tasks
                    UnblockDependents(task);
                }
            }

            if (owner != null) task.Owner = owner;
            if (priority != null) task.Priority = priority;

            SaveTask(task);
            return task;
        }
    }

    public bool Delete(string taskId)
    {
        var path = TaskPath(taskId);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    // ── Queries ──────────────────────────────────────────────────────

    public List<TaskItem> ListTasks(string? status = null, string? owner = null,
        string? priority = null, bool sortByPriority = false)
    {
        var dir = TasksDir();
        var files = Directory.GetFiles(dir, "task-*.json");
        var tasks = new List<TaskItem>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var task = JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
                if (task == null) continue;

                if (status != null && task.Status != status) continue;
                if (owner != null && task.Owner != owner) continue;
                if (priority != null && task.Priority != priority) continue;

                tasks.Add(task);
            }
            catch { }
        }

        if (sortByPriority)
        {
            var priorityOrder = new Dictionary<string, int>
            {
                ["urgent"] = 0, ["high"] = 1, ["medium"] = 2, ["low"] = 3
            };
            tasks.Sort((a, b) =>
                priorityOrder.GetValueOrDefault(a.Priority, 9)
                    .CompareTo(priorityOrder.GetValueOrDefault(b.Priority, 9)));
        }

        return tasks;
    }

    public TaskSummaryViewModel GetStatistics()
    {
        var all = ListTasks();
        return new TaskSummaryViewModel
        {
            Total = all.Count,
            Pending = all.Count(t => t.Status == "pending"),
            InProgress = all.Count(t => t.Status == "in_progress"),
            Completed = all.Count(t => t.Status == "completed"),
            Blocked = all.Count(t => t.Status == "blocked"),
        };
    }

    // ── Dependency helpers ───────────────────────────────────────────

    private bool HasUnresolvedDeps(List<string> blockedBy)
    {
        return blockedBy.Any(depId =>
        {
            var dep = LoadTask(depId);
            return dep == null || dep.Status != "completed";
        });
    }

    private void UnblockDependents(TaskItem completedTask)
    {
        foreach (var dependentId in completedTask.Blocks)
        {
            var dependent = LoadTask(dependentId);
            if (dependent == null) continue;

            dependent.BlockedBy.Remove(completedTask.Id);
            if (dependent.Status == "blocked" && !HasUnresolvedDeps(dependent.BlockedBy))
            {
                dependent.Status = "pending";
            }
            SaveTask(dependent);
        }
    }

    private void ValidateNoCycles(string taskId, List<string> blockedBy)
    {
        var visited = new HashSet<string> { taskId };
        var stack = new Stack<string>(blockedBy);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == taskId)
                throw new InvalidOperationException($"Circular dependency detected involving task '{taskId}'");
            if (!visited.Add(current)) continue;

            var task = LoadTask(current);
            if (task != null)
            {
                foreach (var dep in task.BlockedBy)
                    stack.Push(dep);
            }
        }
    }
}
