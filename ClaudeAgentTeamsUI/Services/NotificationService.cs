using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class NotificationService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<NotificationService> _logger;
    private ConcurrentDictionary<string, Notification> _notifications = new();
    private bool _loaded;

    public NotificationService(JsonDataStore store, ILogger<NotificationService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var notifications = await _store.LoadAsync<List<Notification>>("notifications.json");
        _notifications = new ConcurrentDictionary<string, Notification>(notifications.ToDictionary(n => n.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("notifications.json", _notifications.Values.ToList());
    }

    public async Task<List<Notification>> GetAllAsync(int limit = 50, int offset = 0)
    {
        await EnsureLoadedAsync();
        return _notifications.Values
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        await EnsureLoadedAsync();
        return _notifications.Values.Count(n => !n.IsRead);
    }

    public async Task<Notification> CreateAsync(string title, string? message = null,
        NotificationCategory category = NotificationCategory.Info,
        string? teamId = null, string? teamEventType = null)
    {
        await EnsureLoadedAsync();
        var notification = new Notification
        {
            Title = title,
            Message = message,
            Category = category,
            TeamId = teamId,
            TeamEventType = teamEventType
        };
        _notifications[notification.Id] = notification;
        await PersistAsync();
        return notification;
    }

    public async Task<bool> MarkReadAsync(string id)
    {
        await EnsureLoadedAsync();
        if (!_notifications.TryGetValue(id, out var n)) return false;
        n.IsRead = true;
        await PersistAsync();
        return true;
    }

    public async Task<int> MarkAllReadAsync()
    {
        await EnsureLoadedAsync();
        int count = 0;
        foreach (var n in _notifications.Values.Where(n => !n.IsRead))
        {
            n.IsRead = true;
            count++;
        }
        if (count > 0) await PersistAsync();
        return count;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureLoadedAsync();
        return _notifications.TryRemove(id, out _);
    }
}
