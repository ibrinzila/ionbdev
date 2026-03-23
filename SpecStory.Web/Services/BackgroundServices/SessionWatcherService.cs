using SpecStory.Web.Services.Providers;

namespace SpecStory.Web.Services.BackgroundServices;

/// <summary>
/// Background service that watches for new AI agent sessions
/// and automatically processes them into markdown/cloud sync.
/// </summary>
public class SessionWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionWatcherService> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();

    public SessionWatcherService(IServiceProvider serviceProvider, ILogger<SessionWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session watcher service starting...");

        SetupFileWatchers();

        // Keep service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        CleanupWatchers();
    }

    private void SetupFileWatchers()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Watch Claude Code sessions
        WatchDirectory(Path.Combine(home, ".claude", "projects"), "*.jsonl");

        // Watch Codex CLI sessions
        WatchDirectory(Path.Combine(home, ".codex", "sessions"), "*.jsonl");

        // Watch Gemini CLI sessions
        WatchDirectory(Path.Combine(home, ".gemini", "tmp"), "*.json");

        // Watch Droid CLI sessions
        WatchDirectory(Path.Combine(home, ".factory", "sessions"), "*.jsonl");
    }

    private void WatchDirectory(string path, string filter)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogDebug("Watch directory does not exist: {Path}", path);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(path, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                               NotifyFilters.FileName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnSessionFileChanged;
            watcher.Created += OnSessionFileChanged;

            _watchers.Add(watcher);
            _logger.LogInformation("Watching directory: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to watch directory: {Path}", path);
        }
    }

    private void OnSessionFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Session file changed: {Path}", e.FullPath);

        // Process the changed session file asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to allow file writing to complete
                await Task.Delay(2000);

                using var scope = _serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                var markdownService = scope.ServiceProvider.GetRequiredService<IMarkdownService>();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();

                var config = configService.GetEffectiveConfig();

                // Generate markdown if local sync is enabled
                if (config.LocalSync.Enabled)
                {
                    _logger.LogInformation("Processing session file: {Path}", e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing session file: {Path}", e.FullPath);
            }
        });
    }

    private void CleanupWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    public override void Dispose()
    {
        CleanupWatchers();
        base.Dispose();
    }
}
