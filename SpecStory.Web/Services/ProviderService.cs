using SpecStory.Web.Models;
using SpecStory.Web.Services.Providers;

namespace SpecStory.Web.Services;

public class ProviderService : IProviderService
{
    private readonly ProviderFactory _providerFactory;

    public ProviderService(ProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public List<ProviderCheckResult> CheckAllProviders()
    {
        return _providerFactory.GetAllProviders()
            .Select(p => p.Check())
            .ToList();
    }

    public ProviderCheckResult CheckProvider(string providerId)
    {
        var provider = _providerFactory.GetProvider(providerId);
        if (provider == null)
        {
            return new ProviderCheckResult
            {
                ProviderId = providerId,
                ProviderName = "Unknown",
                Error = $"Provider '{providerId}' not found"
            };
        }

        return provider.Check();
    }

    public List<string> GetAvailableProviderIds()
    {
        return _providerFactory.GetProviderIds().ToList();
    }
}
