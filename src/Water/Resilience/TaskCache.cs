using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Water.Core;

/// <summary>
/// Abstract interface for task result caches.
/// </summary>
public interface ITaskCache
{
    object? Get(string key);
    void Set(string key, object value, double? ttlSeconds = null);
    bool Has(string key);
    void Clear();
}

/// <summary>
/// Simple in-memory cache with optional per-entry TTL.
/// </summary>
public class InMemoryCache : ITaskCache
{
    private readonly Dictionary<string, (object Value, long? ExpiresAt)> _store = new();
    private readonly object _lock = new();

    private bool IsExpired(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
            return true;
        if (entry.ExpiresAt.HasValue && Environment.TickCount64 >= entry.ExpiresAt.Value)
        {
            _store.Remove(key);
            return true;
        }
        return false;
    }

    public object? Get(string key)
    {
        lock (_lock)
        {
            if (IsExpired(key)) return null;
            return _store[key].Value;
        }
    }

    public void Set(string key, object value, double? ttlSeconds = null)
    {
        lock (_lock)
        {
            var expiresAt = ttlSeconds.HasValue ? Environment.TickCount64 + (long)(ttlSeconds.Value * 1000) : (long?)null;
            _store[key] = (value, expiresAt);
        }
    }

    public bool Has(string key)
    {
        lock (_lock) { return !IsExpired(key); }
    }

    public void Clear()
    {
        lock (_lock) { _store.Clear(); }
    }
}

/// <summary>
/// Helper for computing deterministic cache keys.
/// </summary>
public static class CacheHelper
{
    public static string ComputeKey(string taskId, object data)
    {
        var payload = JsonSerializer.Serialize(new { task_id = taskId, data }, new JsonSerializerOptions { WriteIndented = false });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}
