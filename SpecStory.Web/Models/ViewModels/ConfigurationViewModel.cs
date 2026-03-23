namespace SpecStory.Web.Models.ViewModels;

public class ConfigurationViewModel
{
    public SpecStoryConfig UserConfig { get; set; } = new();
    public SpecStoryConfig? ProjectConfig { get; set; }
    public SpecStoryConfig EffectiveConfig { get; set; } = new();
    public string? UserConfigPath { get; set; }
    public string? ProjectConfigPath { get; set; }
    public string? StatusMessage { get; set; }
    public bool IsSuccess { get; set; }
}
