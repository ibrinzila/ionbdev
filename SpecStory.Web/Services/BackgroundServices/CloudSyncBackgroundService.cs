namespace SpecStory.Web.Services.BackgroundServices;

/// <summary>
/// Background service that periodically syncs sessions to SpecStory Cloud.
/// Implements debounce logic (minimum 10s between syncs for the same session).
/// </summary>
public class CloudSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CloudSyncBackgroundService> _logger;
    private readonly Dictionary<string, DateTime> _lastSyncTimes = new();
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public CloudSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CloudSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cloud sync background service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPendingSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cloud sync background service");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task SyncPendingSessionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();
        var config = configService.GetEffectiveConfig();

        if (!config.CloudSync.Enabled)
            return;

        var cloudSync = scope.ServiceProvider.GetRequiredService<ICloudSyncService>();
        if (!await cloudSync.IsAuthenticatedAsync())
            return;

        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var sessions = await sessionService.ListSessionsAsync();

        foreach (var sessionMeta in sessions)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Debounce check
            if (_lastSyncTimes.TryGetValue(sessionMeta.SessionId, out var lastSync) &&
                DateTime.UtcNow - lastSync < DebounceInterval)
            {
                continue;
            }

            try
            {
                var session = await sessionService.GetSessionAsync(
                    sessionMeta.SessionId, sessionMeta.ProviderId);

                if (session != null)
                {
                    var result = await cloudSync.SyncSessionAsync(
                        session,
                        session.WorkspaceRoot ?? "unknown",
                        Path.GetFileName(session.WorkspaceRoot ?? "unknown"));

                    _lastSyncTimes[sessionMeta.SessionId] = DateTime.UtcNow;

                    if (result.HasErrors)
                    {
                        _logger.LogWarning("Failed to sync session {Id}: {Errors}",
                            sessionMeta.SessionId, string.Join(", ", result.Errors));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error syncing session {Id}", sessionMeta.SessionId);
            }
        }
    }
}
