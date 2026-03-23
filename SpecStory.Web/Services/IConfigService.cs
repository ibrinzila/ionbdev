using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public interface IConfigService
{
    SpecStoryConfig GetUserConfig();
    SpecStoryConfig? GetProjectConfig(string? projectPath = null);
    SpecStoryConfig GetEffectiveConfig(string? projectPath = null);
    void SaveUserConfig(SpecStoryConfig config);
    void SaveProjectConfig(SpecStoryConfig config, string? projectPath = null);
    string GetUserConfigPath();
    string GetProjectConfigPath(string? projectPath = null);
}
