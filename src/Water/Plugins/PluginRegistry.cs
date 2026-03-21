using Water.Core;

namespace Water.Plugins;

public enum PluginType
{
    Middleware,
    Storage,
    Provider,
    Trigger,
    Custom
}

/// <summary>
/// Base interface for Water plugins.
/// </summary>
public interface IWaterPlugin
{
    string Name { get; }
    string Version { get; }
    PluginType Type { get; }
    Task InitializeAsync(Dictionary<string, object?> config);
    Task ShutdownAsync();
}

/// <summary>
/// Registry for managing Water plugins.
/// </summary>
public class PluginRegistry
{
    private readonly Dictionary<string, IWaterPlugin> _plugins = new();

    public async Task RegisterAsync(IWaterPlugin plugin, Dictionary<string, object?>? config = null)
    {
        await plugin.InitializeAsync(config ?? new());
        _plugins[plugin.Name] = plugin;
    }

    public IWaterPlugin? Get(string name) => _plugins.TryGetValue(name, out var p) ? p : null;

    public T? Get<T>(string name) where T : class, IWaterPlugin => Get(name) as T;

    public IReadOnlyList<IWaterPlugin> All => _plugins.Values.ToList();

    public List<IWaterPlugin> ByType(PluginType type) => _plugins.Values.Where(p => p.Type == type).ToList();

    public async Task ShutdownAllAsync()
    {
        foreach (var plugin in _plugins.Values)
            await plugin.ShutdownAsync();
    }
}
