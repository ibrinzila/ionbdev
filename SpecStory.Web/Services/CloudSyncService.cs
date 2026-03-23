using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public class CloudSyncService : ICloudSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ISessionService _sessionService;
    private readonly IMarkdownService _markdownService;
    private readonly ILogger<CloudSyncService> _logger;

    private string? _refreshToken;
    private string? _accessToken;

    public CloudSyncService(
        HttpClient httpClient,
        IConfigService configService,
        ISessionService sessionService,
        IMarkdownService markdownService,
        ILogger<CloudSyncService> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _sessionService = sessionService;
        _markdownService = markdownService;
        _logger = logger;

        LoadAuthToken();
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_refreshToken));
    }

    public Task<string?> GetLoginUrlAsync()
    {
        var config = _configService.GetEffectiveConfig();
        var url = $"{config.CloudSync.CloudUrl}/cli-login";
        return Task.FromResult<string?>(url);
    }

    public Task<bool> LoginWithRefreshTokenAsync(string refreshToken)
    {
        _refreshToken = refreshToken;
        SaveAuthToken(refreshToken);
        return Task.FromResult(true);
    }

    public Task LogoutAsync()
    {
        _refreshToken = null;
        _accessToken = null;

        var authPath = GetAuthFilePath();
        if (File.Exists(authPath))
            File.Delete(authPath);

        return Task.CompletedTask;
    }

    public async Task<CloudSyncResult> SyncSessionAsync(SessionData session, string projectId,
        string projectName)
    {
        var result = new CloudSyncResult();

        if (!await IsAuthenticatedAsync())
        {
            result.Errored = 1;
            result.Errors.Add("Not authenticated. Please log in first.");
            return result;
        }

        try
        {
            var config = _configService.GetEffectiveConfig();
            var markdown = _markdownService.GenerateMarkdown(session);
            var rawData = JsonSerializer.Serialize(session);

            var payload = new
            {
                projectId,
                projectName,
                name = session.DisplayName,
                markdown,
                rawData,
                metadata = new
                {
                    clientName = "specstory-web",
                    clientVersion = "1.0.0",
                    agentName = session.Provider.Name,
                    deviceId = GetDeviceId()
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken ?? _refreshToken);

            var response = await _httpClient.PostAsync(
                $"{config.CloudSync.CloudUrl}/sync/session", content);

            if (response.IsSuccessStatusCode)
            {
                result.Created = 1;
            }
            else
            {
                result.Errored = 1;
                result.Errors.Add($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            result.Errored = 1;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Failed to sync session {SessionId}", session.SessionId);
        }

        return result;
    }

    public async Task<CloudSyncResult> SyncAllSessionsAsync(string? providerId = null,
        string? projectPath = null)
    {
        var result = new CloudSyncResult();

        if (!await IsAuthenticatedAsync())
        {
            result.Errored = 1;
            result.Errors.Add("Not authenticated.");
            return result;
        }

        var sessions = await _sessionService.GetAllSessionsAsync(projectPath);

        foreach (var session in sessions)
        {
            if (providerId != null && session.Provider.Id != providerId)
            {
                result.Skipped++;
                continue;
            }

            var syncResult = await SyncSessionAsync(
                session,
                session.WorkspaceRoot ?? "unknown",
                Path.GetFileName(session.WorkspaceRoot ?? "unknown"));

            result.Created += syncResult.Created;
            result.Updated += syncResult.Updated;
            result.Errored += syncResult.Errored;
            result.Errors.AddRange(syncResult.Errors);
        }

        return result;
    }

    private void LoadAuthToken()
    {
        var authPath = GetAuthFilePath();
        if (!File.Exists(authPath)) return;

        try
        {
            var json = File.ReadAllText(authPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("refresh_token", out var token))
                _refreshToken = token.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load auth token");
        }
    }

    private void SaveAuthToken(string refreshToken)
    {
        var authPath = GetAuthFilePath();
        var dir = Path.GetDirectoryName(authPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(new { refresh_token = refreshToken });
        File.WriteAllText(authPath, json);
    }

    private static string GetAuthFilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".specstory", "cli", ".auth.json");
    }

    private static string GetDeviceId()
    {
        var idPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".specstory", "cli", ".device-id");

        if (File.Exists(idPath))
            return File.ReadAllText(idPath).Trim();

        var id = Guid.NewGuid().ToString();
        var dir = Path.GetDirectoryName(idPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(idPath, id);
        return id;
    }
}
