using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public interface IProviderService
{
    List<ProviderCheckResult> CheckAllProviders();
    ProviderCheckResult CheckProvider(string providerId);
    List<string> GetAvailableProviderIds();
}
