using SpecStory.Web.Models;
using SpecStory.Web.Models.ViewModels;

namespace SpecStory.Web.Services;

public interface ISessionService
{
    Task<List<SessionMetadata>> ListSessionsAsync(string? providerId = null, string? projectPath = null);
    Task<SessionData?> GetSessionAsync(string sessionId, string? providerId = null);
    Task<List<SessionData>> GetAllSessionsAsync(string? projectPath = null);
    SessionStatistics CalculateStatistics(SessionData session);
    Task<SessionListViewModel> GetSessionListViewModelAsync(string? providerId, string? search,
        string sortBy, int page, int pageSize);
}
