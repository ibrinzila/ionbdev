using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class ConfigService
{
    private readonly JsonDataStore _store;
    private AppConfig? _config;

    public ConfigService(JsonDataStore store)
    {
        _store = store;
    }

    public async Task<AppConfig> GetAsync()
    {
        _config ??= await _store.LoadAsync<AppConfig>("config.json");
        return _config;
    }

    public async Task SaveAsync(AppConfig config)
    {
        _config = config;
        await _store.SaveAsync("config.json", config);
    }

    public async Task UpdateAsync(Action<AppConfig> update)
    {
        var config = await GetAsync();
        update(config);
        await SaveAsync(config);
    }
}
