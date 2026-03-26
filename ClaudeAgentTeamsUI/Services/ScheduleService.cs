using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;
using Cronos;

namespace ClaudeAgentTeamsUI.Services;

public class ScheduleService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<ScheduleService> _logger;
    private ConcurrentDictionary<string, Schedule> _schedules = new();
    private bool _loaded;

    public ScheduleService(JsonDataStore store, ILogger<ScheduleService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var schedules = await _store.LoadAsync<List<Schedule>>("schedules.json");
        _schedules = new ConcurrentDictionary<string, Schedule>(schedules.ToDictionary(s => s.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("schedules.json", _schedules.Values.ToList());
    }

    public async Task<List<Schedule>> GetAllAsync(string? teamFilter = null)
    {
        await EnsureLoadedAsync();
        var query = _schedules.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(teamFilter))
            query = query.Where(s => s.TeamName == teamFilter);
        return query.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    public async Task<Schedule?> GetByIdAsync(string id)
    {
        await EnsureLoadedAsync();
        return _schedules.GetValueOrDefault(id);
    }

    public async Task<Schedule> CreateAsync(string teamName, string label, string cronExpression,
        ScheduleLaunchConfig? launchConfig = null)
    {
        await EnsureLoadedAsync();

        var schedule = new Schedule
        {
            TeamName = teamName,
            Label = label,
            CronExpression = cronExpression,
            LaunchConfig = launchConfig ?? new ScheduleLaunchConfig()
        };

        // Calculate next run
        try
        {
            var cron = CronExpression.Parse(cronExpression);
            schedule.NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression: {Cron}", cronExpression);
        }

        _schedules[schedule.Id] = schedule;
        await PersistAsync();
        _logger.LogInformation("Created schedule {Label} for team {Team}", label, teamName);
        return schedule;
    }

    public async Task<Schedule?> UpdateAsync(string id, Action<Schedule> update)
    {
        await EnsureLoadedAsync();
        if (!_schedules.TryGetValue(id, out var schedule)) return null;
        update(schedule);
        schedule.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
        return schedule;
    }

    public async Task<Schedule?> PauseAsync(string id)
    {
        return await UpdateAsync(id, s => s.Status = ScheduleStatus.Paused);
    }

    public async Task<Schedule?> ResumeAsync(string id)
    {
        return await UpdateAsync(id, s =>
        {
            s.Status = ScheduleStatus.Active;
            try
            {
                var cron = CronExpression.Parse(s.CronExpression);
                s.NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow);
            }
            catch { }
        });
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureLoadedAsync();
        var removed = _schedules.TryRemove(id, out _);
        if (removed) await PersistAsync();
        return removed;
    }

    public async Task<ScheduleRun> RecordRunAsync(string scheduleId, ScheduleRunStatus status,
        int? exitCode = null, string? output = null, int tokensUsed = 0, decimal costUsd = 0)
    {
        await EnsureLoadedAsync();
        if (!_schedules.TryGetValue(scheduleId, out var schedule))
            throw new InvalidOperationException($"Schedule {scheduleId} not found");

        var run = new ScheduleRun
        {
            ScheduleId = scheduleId,
            Status = status,
            CompletedAt = status is ScheduleRunStatus.Completed or ScheduleRunStatus.Failed
                ? DateTime.UtcNow : null,
            ExitCode = exitCode,
            OutputSummary = output,
            TokensUsed = tokensUsed,
            CostUsd = costUsd
        };

        schedule.Runs.Add(run);
        schedule.LastRunAt = DateTime.UtcNow;

        if (status == ScheduleRunStatus.Failed)
            schedule.ConsecutiveFailures++;
        else if (status == ScheduleRunStatus.Completed)
            schedule.ConsecutiveFailures = 0;

        if (schedule.ConsecutiveFailures >= schedule.MaxConsecutiveFailures)
            schedule.Status = ScheduleStatus.Paused;

        try
        {
            var cron = CronExpression.Parse(schedule.CronExpression);
            schedule.NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow);
        }
        catch { }

        await PersistAsync();
        return run;
    }
}
