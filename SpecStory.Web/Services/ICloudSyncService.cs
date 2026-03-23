using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public interface ICloudSyncService
{
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetLoginUrlAsync();
    Task<bool> LoginWithRefreshTokenAsync(string refreshToken);
    Task LogoutAsync();
    Task<CloudSyncResult> SyncSessionAsync(SessionData session, string projectId, string projectName);
    Task<CloudSyncResult> SyncAllSessionsAsync(string? providerId = null, string? projectPath = null);
}
