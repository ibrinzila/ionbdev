using SpecStory.Web.Models;
using SpecStory.Web.Models.ViewModels;
using SpecStory.Web.Services.Providers;

namespace SpecStory.Web.Services;

public class SessionService : ISessionService
{
    private readonly ProviderFactory _providerFactory;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ProviderFactory providerFactory, ILogger<SessionService> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<List<SessionMetadata>> ListSessionsAsync(string? providerId = null,
        string? projectPath = null)
    {
        var allMetadata = new List<SessionMetadata>();
        var path = projectPath ?? Directory.GetCurrentDirectory();

        var providers = providerId != null
            ? new[] { _providerFactory.GetProvider(providerId) }.Where(p => p != null).Cast<IAgentProvider>()
            : _providerFactory.GetAllProviders();

        foreach (var provider in providers)
        {
            try
            {
                var sessions = await provider.ListSessionsAsync(path);
                allMetadata.AddRange(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list sessions for provider {Provider}", provider.Id);
            }
        }

        return allMetadata.OrderByDescending(s => s.CreatedAt).ToList();
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId, string? providerId = null)
    {
        var path = Directory.GetCurrentDirectory();

        if (providerId != null)
        {
            var provider = _providerFactory.GetProvider(providerId);
            if (provider != null)
                return await provider.GetSessionAsync(path, sessionId);
            return null;
        }

        // Search all providers
        foreach (var provider in _providerFactory.GetAllProviders())
        {
            try
            {
                var session = await provider.GetSessionAsync(path, sessionId);
                if (session != null)
                    return session;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching provider {Provider} for session {Session}",
                    provider.Id, sessionId);
            }
        }

        return null;
    }

    public async Task<List<SessionData>> GetAllSessionsAsync(string? projectPath = null)
    {
        var allSessions = new List<SessionData>();
        var path = projectPath ?? Directory.GetCurrentDirectory();

        foreach (var provider in _providerFactory.GetAllProviders())
        {
            try
            {
                var sessions = await provider.GetSessionsAsync(path);
                allSessions.AddRange(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load sessions for provider {Provider}", provider.Id);
            }
        }

        return allSessions.OrderByDescending(s => s.CreatedAt).ToList();
    }

    public SessionStatistics CalculateStatistics(SessionData session)
    {
        var stats = new SessionStatistics
        {
            ExchangeCount = session.Exchanges.Count,
            MessageCount = session.TotalMessages,
            UserMessageCount = session.Exchanges
                .SelectMany(e => e.Messages)
                .Count(m => m.Role == "user"),
            AgentMessageCount = session.Exchanges
                .SelectMany(e => e.Messages)
                .Count(m => m.Role == "agent"),
            ToolUseCount = session.TotalToolUses,
            TotalInputTokens = session.TotalInputTokens,
            TotalOutputTokens = session.TotalOutputTokens,
            TotalTokens = session.TotalTokens,
            Duration = session.Duration
        };

        // Tool usage by type
        var toolMessages = session.Exchanges
            .SelectMany(e => e.Messages)
            .Where(m => m.Tool != null);

        stats.ToolUsageByType = toolMessages
            .GroupBy(m => m.Tool!.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // Models used
        stats.ModelsUsed = session.Exchanges
            .SelectMany(e => e.Messages)
            .Where(m => !string.IsNullOrEmpty(m.Model))
            .Select(m => m.Model!)
            .Distinct()
            .ToList();

        // Files referenced
        stats.FilesReferenced = session.Exchanges
            .SelectMany(e => e.Messages)
            .Where(m => m.PathHints != null)
            .SelectMany(m => m.PathHints!)
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        return stats;
    }

    public async Task<SessionListViewModel> GetSessionListViewModelAsync(string? providerId,
        string? search, string sortBy, int page, int pageSize)
    {
        var allSessions = await ListSessionsAsync(providerId);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            allSessions = allSessions
                .Where(s => (s.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            s.SessionId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            (s.WorkspaceRoot?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        // Apply sorting
        allSessions = sortBy switch
        {
            "created_asc" => allSessions.OrderBy(s => s.CreatedAt).ToList(),
            "name_asc" => allSessions.OrderBy(s => s.Name).ToList(),
            "name_desc" => allSessions.OrderByDescending(s => s.Name).ToList(),
            "provider_asc" => allSessions.OrderBy(s => s.ProviderName).ToList(),
            _ => allSessions.OrderByDescending(s => s.CreatedAt).ToList()
        };

        var totalCount = allSessions.Count;
        var paged = allSessions.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new SessionListViewModel
        {
            Sessions = paged,
            FilterProvider = providerId,
            SearchQuery = search,
            SortBy = sortBy,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            AvailableProviders = _providerFactory.GetProviderIds().ToList()
        };
    }
}
