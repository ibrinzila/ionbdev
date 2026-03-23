using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codapter.BackendPi;
using Codapter.Core.Models;
using Codapter.Core.Services;
using Microsoft.Extensions.Logging;

namespace Codapter.Web.Services;

/// <summary>
/// Main JSON-RPC handler that bridges client requests to the backend.
/// Port of packages/core/src/app-server.ts
/// </summary>
public class AppServerConnection
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IBackend _backend;
    private readonly ThreadRegistry _threadRegistry;
    private readonly InMemoryConfigStore _configStore;
    private readonly CommandExecManager _commandExec;
    private readonly ILogger<AppServerConnection> _logger;
    private readonly bool _collabEnabled;

    private CollabManager? _collabManager;
    private bool _initialized;
    private ClientInfo? _clientInfo;

    // Per-thread state
    private readonly ConcurrentDictionary<string, ThreadRuntimeState> _threadStates = new();

    // Notification sink for streaming to client
    public event Action<JsonRpcNotification>? OnNotification;

    public AppServerConnection(
        IBackend backend,
        ThreadRegistry threadRegistry,
        InMemoryConfigStore configStore,
        CommandExecManager commandExec,
        ILogger<AppServerConnection> logger,
        bool collabEnabled = false)
    {
        _backend = backend;
        _threadRegistry = threadRegistry;
        _configStore = configStore;
        _commandExec = commandExec;
        _logger = logger;
        _collabEnabled = collabEnabled;
    }

    public async Task<object> HandleRequestAsync(
        string method, JsonElement? @params, CancellationToken ct = default)
    {
        // Pre-init check
        if (!_initialized && method != "initialize")
        {
            throw new JsonRpcException(JsonRpcErrorCodes.NotInitialized,
                "Server not initialized. Send 'initialize' first.");
        }

        return method switch
        {
            "initialize" => HandleInitialize(@params),
            "auth/status" => HandleAuthStatus(),
            "auth/login" => await HandleAuthLogin(@params, ct),
            "auth/logout" => HandleAuthLogout(),

            "thread/start" => await HandleThreadStart(@params, ct),
            "thread/resume" => await HandleThreadResume(@params, ct),
            "thread/fork" => await HandleThreadFork(@params, ct),
            "thread/list" => await HandleThreadList(ct),
            "thread/archive" => await HandleThreadArchive(@params, ct),

            "turn/start" => await HandleTurnStart(@params, ct),
            "turn/interrupt" => await HandleTurnInterrupt(@params, ct),

            "model/list" => await HandleModelList(ct),
            "model/current" => await HandleModelCurrent(@params, ct),

            "config/read" => HandleConfigRead(),
            "config/value/write" => HandleConfigValueWrite(@params),

            "command/exec" => await HandleCommandExec(@params, ct),
            "command/exec/stdin" => await HandleCommandExecStdin(@params, ct),
            "command/exec/interrupt" => HandleCommandExecInterrupt(@params),

            "collaborationMode/list" => new { modes = Array.Empty<object>() },
            "experimentalFeature/list" => new { features = Array.Empty<object>() },
            "mcpServerStatus/list" => new { servers = Array.Empty<object>() },

            _ => throw new JsonRpcException(JsonRpcErrorCodes.MethodNotFound,
                $"Method '{method}' is not supported")
        };
    }

    #region Initialize

    private object HandleInitialize(JsonElement? @params)
    {
        if (_initialized)
            throw new JsonRpcException(JsonRpcErrorCodes.AlreadyInitialized,
                "Server already initialized");

        if (@params.HasValue)
        {
            _clientInfo = JsonSerializer.Deserialize<InitializeParams>(@params.Value, JsonOptions)?.ClientInfo;
        }

        _initialized = true;

        return new InitializeResponse
        {
            Capabilities = new ServerCapabilities
            {
                SupportsCollaboration = _collabEnabled
            }
        };
    }

    #endregion

    #region Auth

    private object HandleAuthStatus()
    {
        return new GetAuthStatusResponse
        {
            Accounts = new List<Account>
            {
                new()
                {
                    Mode = AuthMode.ApiKey,
                    IsLoggedIn = true,
                    PlanType = Models.PlanType.Pro
                }
            }
        };
    }

    private async Task<object> HandleAuthLogin(JsonElement? @params, CancellationToken ct)
    {
        // Codapter delegates auth to the backend; accept any login
        return new { success = true };
    }

    private object HandleAuthLogout()
    {
        return new { success = true };
    }

    #endregion

    #region Threads

    private async Task<object> HandleThreadStart(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<ThreadStartParams>(@params);

        var session = await _backend.CreateSessionAsync(p.Model, p.Path, ct);

        var entry = await _threadRegistry.CreateAsync(new CreateThreadRegistryEntry
        {
            BackendSessionId = session.SessionId,
            Model = p.Model,
            Path = p.Path
        }, ct);

        var turnId = Guid.NewGuid().ToString();

        // Set up turn state machine
        var tsm = new TurnStateMachine(entry.ThreadId, turnId,
            n => EmitNotification(n.Method, n.Params), _collabEnabled);

        var state = new ThreadRuntimeState
        {
            SessionId = session.SessionId,
            CurrentTurnId = turnId,
            TurnStateMachine = tsm
        };
        _threadStates[entry.ThreadId] = state;

        // Subscribe to backend events
        state.EventSubscription = _backend.OnEvent(session.SessionId, evt =>
        {
            tsm.HandleEvent(evt);
        });

        // Send the initial prompt
        var text = p.Input.Text ?? "";
        await _backend.PromptAsync(session.SessionId, text, p.Input.Images, ct);

        return new ThreadStartResponse
        {
            ThreadId = entry.ThreadId,
            TurnId = turnId
        };
    }

    private async Task<object> HandleThreadResume(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<ThreadResumeParams>(@params);

        var entry = await _threadRegistry.GetAsync(p.ThreadId, ct)
            ?? throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams,
                $"Thread {p.ThreadId} not found");

        if (entry.BackendSessionId != null)
        {
            var session = await _backend.ResumeSessionAsync(entry.BackendSessionId, ct);
            var state = new ThreadRuntimeState { SessionId = session.SessionId };
            _threadStates[p.ThreadId] = state;
        }

        var history = entry.BackendSessionId != null
            ? await _backend.GetHistoryAsync(entry.BackendSessionId, ct)
            : new List<BackendMessage>();

        var thread = new Core.Models.Thread
        {
            ThreadId = entry.ThreadId,
            Title = entry.Title,
            Status = entry.Status,
            Model = entry.Model,
            Path = entry.Path,
            Git = entry.Git,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            Turns = history.Select((m, i) => new Turn
            {
                TurnId = $"turn-{i}",
                Status = TurnStatus.Completed,
                Items = new List<ThreadItem>
                {
                    new()
                    {
                        Id = $"msg-{i}",
                        Type = m.Role == "user"
                            ? ThreadItemType.UserMessage
                            : ThreadItemType.AgentMessage,
                        Text = m.Text
                    }
                }
            }).ToList()
        };

        return new ThreadResumeResponse { Thread = thread };
    }

    private async Task<object> HandleThreadFork(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<ThreadForkParams>(@params);

        var entry = await _threadRegistry.GetAsync(p.ThreadId, ct)
            ?? throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams,
                $"Thread {p.ThreadId} not found");

        BackendSessionInfo? forkedSession = null;
        if (entry.BackendSessionId != null)
        {
            forkedSession = await _backend.ForkSessionAsync(
                entry.BackendSessionId, p.TurnIndex, ct);
        }

        var newEntry = await _threadRegistry.CreateAsync(new CreateThreadRegistryEntry
        {
            BackendSessionId = forkedSession?.SessionId,
            Title = entry.Title,
            Model = entry.Model,
            Path = entry.Path,
            Git = entry.Git
        }, ct);

        return new ThreadForkResponse { ThreadId = newEntry.ThreadId };
    }

    private async Task<object> HandleThreadList(CancellationToken ct)
    {
        var entries = await _threadRegistry.ListAsync(ct);

        return new ThreadListResponse
        {
            Threads = entries.Select(e => new Core.Models.Thread
            {
                ThreadId = e.ThreadId,
                Title = e.Title,
                Status = e.Status,
                Model = e.Model,
                Path = e.Path,
                Git = e.Git,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            }).ToList()
        };
    }

    private async Task<object> HandleThreadArchive(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<ThreadArchiveParams>(@params);

        await _threadRegistry.UpdateAsync(p.ThreadId, new UpdateThreadRegistryEntry
        {
            Status = ThreadStatus.Archived
        }, ct);

        // Dispose backend session if active
        if (_threadStates.TryRemove(p.ThreadId, out var state))
        {
            state.EventSubscription?.Dispose();
            await _backend.DisposeSessionAsync(state.SessionId, ct);
        }

        return new { success = true };
    }

    #endregion

    #region Turns

    private async Task<object> HandleTurnStart(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<TurnStartParams>(@params);

        if (!_threadStates.TryGetValue(p.ThreadId, out var state))
            throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams,
                $"Thread {p.ThreadId} not active");

        var turnId = Guid.NewGuid().ToString();
        var tsm = new TurnStateMachine(p.ThreadId, turnId,
            n => EmitNotification(n.Method, n.Params), _collabEnabled);

        state.CurrentTurnId = turnId;
        state.TurnStateMachine = tsm;

        // Re-subscribe events to new TSM
        state.EventSubscription?.Dispose();
        state.EventSubscription = _backend.OnEvent(state.SessionId, evt =>
        {
            tsm.HandleEvent(evt);
        });

        var text = p.Input.Text ?? "";
        await _backend.PromptAsync(state.SessionId, text, p.Input.Images, ct);

        return new TurnStartResponse { TurnId = turnId };
    }

    private async Task<object> HandleTurnInterrupt(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<TurnInterruptParams>(@params);

        if (!_threadStates.TryGetValue(p.ThreadId, out var state))
            throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams,
                $"Thread {p.ThreadId} not active");

        await _backend.AbortAsync(state.SessionId, ct);

        return new { success = true };
    }

    #endregion

    #region Models

    private async Task<object> HandleModelList(CancellationToken ct)
    {
        return await _backend.ListModelsAsync(ct);
    }

    private async Task<object> HandleModelCurrent(JsonElement? @params, CancellationToken ct)
    {
        var threadId = GetStringParam(@params, "threadId");
        if (threadId == null || !_threadStates.TryGetValue(threadId, out var state))
        {
            return new ModelInfo { Id = "default", Name = "Default" };
        }

        return await _backend.GetCurrentModelAsync(state.SessionId, ct);
    }

    #endregion

    #region Config

    private object HandleConfigRead()
    {
        return _configStore.Read();
    }

    private object HandleConfigValueWrite(JsonElement? @params)
    {
        if (!@params.HasValue)
            throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams, "Missing params");

        var p = Deserialize<ConfigWriteParams>(@params);
        return _configStore.WriteBatch(p.Version, p.Edits);
    }

    #endregion

    #region Command Execution

    private async Task<object> HandleCommandExec(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<CommandExecParams>(@params);

        if (p.Tty)
            throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams,
                "TTY mode is not supported");

        return await _commandExec.ExecuteAsync(new CommandExecOptions
        {
            Command = p.Command,
            Args = p.Args,
            Cwd = p.Cwd,
            Env = p.Env,
            TimeoutMs = p.Timeout,
            Streaming = p.Streaming,
            ProcessId = p.ProcessId
        }, ct);
    }

    private async Task<object> HandleCommandExecStdin(JsonElement? @params, CancellationToken ct)
    {
        var p = Deserialize<CommandExecStdinParams>(@params);
        await _commandExec.WriteStdinAsync(p.ProcessId, p.Data, ct);
        return new { success = true };
    }

    private object HandleCommandExecInterrupt(JsonElement? @params)
    {
        var p = Deserialize<CommandExecInterruptParams>(@params);
        _commandExec.Terminate(p.ProcessId);
        return new { success = true };
    }

    #endregion

    #region Helpers

    private void EmitNotification(string method, object? @params)
    {
        OnNotification?.Invoke(new JsonRpcNotification
        {
            Method = method,
            Params = @params != null
                ? JsonSerializer.SerializeToElement(@params, JsonOptions)
                : null
        });
    }

    private static T Deserialize<T>(JsonElement? @params)
    {
        if (!@params.HasValue)
            throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams, "Missing params");

        return JsonSerializer.Deserialize<T>(@params.Value, JsonOptions)
            ?? throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams, "Invalid params");
    }

    private static string? GetStringParam(JsonElement? @params, string key)
    {
        if (!@params.HasValue) return null;
        return @params.Value.TryGetProperty(key, out var val) ? val.GetString() : null;
    }

    #endregion
}

public class JsonRpcException : Exception
{
    public int Code { get; }

    public JsonRpcException(int code, string message) : base(message)
    {
        Code = code;
    }
}

internal class ThreadRuntimeState
{
    public string SessionId { get; set; } = default!;
    public string? CurrentTurnId { get; set; }
    public TurnStateMachine? TurnStateMachine { get; set; }
    public IDisposable? EventSubscription { get; set; }
}
