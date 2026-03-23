using System.Collections.Concurrent;
using System.Text.Json;
using Codapter.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codapter.BackendPi;

/// <summary>
/// IBackend implementation using Pi coding agent as the backend.
/// Port of packages/backend-pi/src/index.ts
/// </summary>

public record PiBackendOptions
{
    public string Command { get; init; } = "npx";
    public string[] Args { get; init; } = { "@mariozechner/pi-coding-agent" };
    public Dictionary<string, string>? Env { get; init; }
    public string? SessionDirectory { get; init; }
    public int IdleTimeoutMs { get; init; } = 300_000; // 5 minutes
    public string? DebugLogFile { get; init; }
}

public class PiBackend : IBackend
{
    private readonly PiBackendOptions _options;
    private readonly ILogger<PiBackend>? _logger;
    private readonly PiBackendStateStore _stateStore;

    private readonly ConcurrentDictionary<string, PiProcessSession> _sessions = new();
    private readonly ConcurrentDictionary<string, Timer> _idleTimers = new();
    private readonly ConcurrentDictionary<string, List<Action<BackendEvent>>> _eventHandlers = new();

    private List<ModelInfo>? _cachedModels;
    private BackendCapabilities? _cachedCapabilities;

    public PiBackend(PiBackendOptions? options = null, ILogger<PiBackend>? logger = null)
    {
        _options = options ?? new PiBackendOptions();
        _logger = logger;

        var sessionDir = _options.SessionDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "codapter", "pi-sessions");
        _stateStore = new PiBackendStateStore(sessionDir);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _stateStore.LoadAsync(ct);
    }

    public async Task<BackendSessionInfo> CreateSessionAsync(
        string? model = null, string? path = null, CancellationToken ct = default)
    {
        var session = CreateProcessSession();
        var sessionId = await session.StartFreshAsync(model, path, ct);

        _sessions[sessionId] = session;
        ResetIdleTimer(sessionId);

        // Wire up events
        session.OnEvent(evt =>
        {
            evt = evt with { SessionId = sessionId };
            EmitEvent(sessionId, evt);
        });

        var now = DateTimeOffset.UtcNow;
        _stateStore.Upsert(new PiBackendSessionRecord
        {
            OpaqueSessionId = sessionId,
            SessionFile = "",
            ModelId = model,
            CreatedAt = now,
            UpdatedAt = now
        });

        return new BackendSessionInfo
        {
            SessionId = sessionId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task<BackendSessionInfo> ResumeSessionAsync(
        string sessionId, CancellationToken ct = default)
    {
        var record = _stateStore.Get(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found in state store");

        var session = CreateProcessSession();
        await session.AttachSessionAsync(sessionId, record.SessionFile, ct);

        _sessions[sessionId] = session;
        ResetIdleTimer(sessionId);

        session.OnEvent(evt =>
        {
            evt = evt with { SessionId = sessionId };
            EmitEvent(sessionId, evt);
        });

        return new BackendSessionInfo
        {
            SessionId = sessionId,
            CreatedAt = record.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<BackendSessionInfo> ForkSessionAsync(
        string sourceSessionId, int? turnIndex = null, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sourceSessionId, out var sourceSession))
            throw new KeyNotFoundException($"Source session {sourceSessionId} not found");

        var newSession = CreateProcessSession();
        var newSessionId = await newSession.ForkSessionAsync(sourceSessionId, turnIndex, ct);

        _sessions[newSessionId] = newSession;
        ResetIdleTimer(newSessionId);

        newSession.OnEvent(evt =>
        {
            evt = evt with { SessionId = newSessionId };
            EmitEvent(newSessionId, evt);
        });

        var now = DateTimeOffset.UtcNow;
        _stateStore.Upsert(new PiBackendSessionRecord
        {
            OpaqueSessionId = newSessionId,
            SessionFile = "",
            CreatedAt = now,
            UpdatedAt = now
        });

        return new BackendSessionInfo
        {
            SessionId = newSessionId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task DisposeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync();
        }

        if (_idleTimers.TryRemove(sessionId, out var timer))
        {
            await timer.DisposeAsync();
        }

        _eventHandlers.TryRemove(sessionId, out _);
    }

    public async Task<List<BackendMessage>> GetHistoryAsync(
        string sessionId, CancellationToken ct = default)
    {
        var session = GetSession(sessionId);
        return await session.GetMessagesAsync(ct);
    }

    public Task<BackendSessionInfo> GetSessionInfoAsync(
        string sessionId, CancellationToken ct = default)
    {
        var record = _stateStore.Get(sessionId);
        if (record == null)
            throw new KeyNotFoundException($"Session {sessionId} not found");

        return Task.FromResult(new BackendSessionInfo
        {
            SessionId = sessionId,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        });
    }

    public async Task PromptAsync(
        string sessionId, string text, List<ImageInput>? images = null,
        CancellationToken ct = default)
    {
        var session = GetSession(sessionId);
        ResetIdleTimer(sessionId);
        await session.PromptAsync(text, images, ct);
    }

    public async Task AbortAsync(string sessionId, CancellationToken ct = default)
    {
        var session = GetSession(sessionId);
        await session.AbortAsync(ct);
    }

    public async Task RespondToElicitationAsync(
        string sessionId, string elicitationId, JsonElement response,
        CancellationToken ct = default)
    {
        var session = GetSession(sessionId);
        await session.RespondToElicitationAsync(elicitationId, response, ct);
    }

    public async Task<ModelListResponse> ListModelsAsync(CancellationToken ct = default)
    {
        if (_cachedModels != null)
        {
            return new ModelListResponse { Models = _cachedModels };
        }

        // Need at least one session to query models
        PiProcessSession? session = _sessions.Values.FirstOrDefault();
        if (session == null)
        {
            session = CreateProcessSession();
            await session.StartFreshAsync(ct: ct);
        }

        _cachedModels = await session.ListModelsAsync(ct);
        return new ModelListResponse { Models = _cachedModels };
    }

    public Task<ModelInfo> GetCurrentModelAsync(
        string sessionId, CancellationToken ct = default)
    {
        var record = _stateStore.Get(sessionId);
        return Task.FromResult(new ModelInfo
        {
            Id = record?.ModelId ?? "default",
            Name = record?.ModelId ?? "Default Model"
        });
    }

    public async Task SetModelAsync(
        string sessionId, string modelId, CancellationToken ct = default)
    {
        var session = GetSession(sessionId);
        await session.SetModelAsync(modelId, ct);
        _stateStore.Update(sessionId, r => { /* modelId updated via record */ });
    }

    public Task<BackendCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        _cachedCapabilities ??= new BackendCapabilities
        {
            SupportsImages = true,
            SupportsParallelTools = true,
            SupportsReasoning = true
        };

        return Task.FromResult(_cachedCapabilities);
    }

    public IDisposable OnEvent(string sessionId, Action<BackendEvent> handler)
    {
        var handlers = _eventHandlers.GetOrAdd(sessionId, _ => new List<Action<BackendEvent>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }

        return new EventUnsubscriber(() =>
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
        });
    }

    private PiProcessSession GetSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Session {sessionId} not found");
        return session;
    }

    private PiProcessSession CreateProcessSession()
    {
        return new PiProcessSession(
            command: _options.Command,
            args: _options.Args,
            env: _options.Env,
            logger: _logger != null
                ? LoggerFactory.Create(b => b.AddProvider(new ForwardingLoggerProvider(_logger)))
                    .CreateLogger<PiProcessSession>()
                : null,
            debugLogFile: _options.DebugLogFile
                ?? Environment.GetEnvironmentVariable("CODAPTER_DEBUG_LOG_FILE"));
    }

    private void ResetIdleTimer(string sessionId)
    {
        if (_idleTimers.TryGetValue(sessionId, out var existing))
        {
            existing.Change(_options.IdleTimeoutMs, Timeout.Infinite);
        }
        else
        {
            var timer = new Timer(async _ =>
            {
                _logger?.LogInformation("Idle timeout for session {SessionId}", sessionId);
                await DisposeSessionAsync(sessionId);
            }, null, _options.IdleTimeoutMs, Timeout.Infinite);

            _idleTimers[sessionId] = timer;
        }
    }

    private void EmitEvent(string sessionId, BackendEvent evt)
    {
        if (!_eventHandlers.TryGetValue(sessionId, out var handlers)) return;

        List<Action<BackendEvent>> snapshot;
        lock (handlers)
        {
            snapshot = new(handlers);
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler(evt);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in event handler for session {SessionId}", sessionId);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (sessionId, _) in _sessions)
        {
            await DisposeSessionAsync(sessionId);
        }
    }

    private class EventUnsubscriber : IDisposable
    {
        private readonly Action _unsubscribe;
        public EventUnsubscriber(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }

    private class ForwardingLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _target;
        public ForwardingLoggerProvider(ILogger target) => _target = target;
        public ILogger CreateLogger(string categoryName) => _target;
        public void Dispose() { }
    }
}
