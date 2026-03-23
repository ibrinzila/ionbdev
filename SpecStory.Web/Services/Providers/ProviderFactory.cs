namespace SpecStory.Web.Services.Providers;

public class ProviderFactory
{
    private readonly Dictionary<string, IAgentProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderFactory(IEnumerable<IAgentProvider> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.Id] = provider;
        }
    }

    public IAgentProvider? GetProvider(string id)
    {
        _providers.TryGetValue(id, out var provider);
        return provider;
    }

    public IEnumerable<IAgentProvider> GetAllProviders() => _providers.Values;

    public IEnumerable<string> GetProviderIds() => _providers.Keys;
}
