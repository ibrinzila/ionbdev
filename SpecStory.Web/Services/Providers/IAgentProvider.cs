using SpecStory.Web.Models;

namespace SpecStory.Web.Services.Providers;

public interface IAgentProvider
{
    string Id { get; }
    string Name { get; }

    ProviderCheckResult Check(string? customCommand = null);
    bool DetectAgent(string projectPath);
    Task<SessionData?> GetSessionAsync(string projectPath, string sessionId, bool debugRaw = false);
    Task<List<SessionData>> GetSessionsAsync(string projectPath, bool debugRaw = false,
        IProgress<int>? progress = null);
    Task<List<SessionMetadata>> ListSessionsAsync(string projectPath);
}
