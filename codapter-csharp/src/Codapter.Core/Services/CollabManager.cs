using Codapter.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codapter.Core.Services;

/// <summary>
/// Manages collaborative sub-agents.
/// Port of packages/core/src/collab-manager.ts
/// </summary>

internal class CollabAgentRuntime
{
    public CollabAgent Agent { get; set; } = default!;
    public string? ActiveTurnId { get; set; }
    public string AssistantText { get; set; } = "";
    public IDisposable? EventSubscription { get; set; }
    public List<TaskCompletionSource<CollabAgentState>> Waiters { get; } = new();
}

public class CollabManager
{
    private readonly IBackend _backend;
    private readonly Action<TurnNotification> _notificationSink;
    private readonly CollabConfig _config;
    private readonly ILogger<CollabManager>? _logger;

    private readonly Dictionary<string, CollabAgentRuntime> _agents = new();
    private readonly HashSet<string> _usedNicknames = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CollabManager(
        IBackend backend,
        Action<TurnNotification> notificationSink,
        CollabConfig? config = null,
        ILogger<CollabManager>? logger = null)
    {
        _backend = backend;
        _notificationSink = notificationSink;
        _config = config ?? new CollabConfig();
        _logger = logger;
    }

    public async Task<SpawnAgentResponse> SpawnAsync(
        SpawnAgentRequest request,
        string parentThreadId,
        int parentDepth,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_agents.Count >= _config.MaxAgents)
                throw new InvalidOperationException("Maximum collab agent count reached");

            var newDepth = parentDepth + 1;
            if (newDepth > _config.MaxDepth)
                throw new InvalidOperationException("Maximum collab depth reached");

            var session = await _backend.CreateSessionAsync(request.Model, ct: ct);
            var agentId = Guid.NewGuid().ToString();
            var nickname = CollabNicknames.GetNextNickname(_usedNicknames);

            var agent = new CollabAgent
            {
                Id = agentId,
                Nickname = nickname,
                Role = request.Role,
                ThreadId = Guid.NewGuid().ToString(),
                SessionId = session.SessionId,
                Depth = newDepth,
                Status = CollabAgentStatus.Running
            };

            var runtime = new CollabAgentRuntime { Agent = agent };

            // Subscribe to backend events for this agent
            runtime.EventSubscription = _backend.OnEvent(session.SessionId, evt =>
            {
                HandleAgentEvent(agentId, evt);
            });

            _agents[agentId] = runtime;

            // Send the initial prompt
            await _backend.PromptAsync(session.SessionId, request.Prompt, ct: ct);

            EmitToolItem("item/started", new
            {
                type = "spawnAgent",
                agentId,
                nickname,
                role = request.Role
            });

            return new SpawnAgentResponse { Agent = agent };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SendInputResponse> SendInputAsync(
        SendInputRequest request, CancellationToken ct = default)
    {
        var runtime = GetRuntime(request.AgentId);

        if (request.Interrupt)
        {
            await _backend.AbortAsync(runtime.Agent.SessionId, ct);
        }

        await _backend.PromptAsync(runtime.Agent.SessionId, request.Message, ct: ct);

        EmitToolItem("item/started", new
        {
            type = "sendInput",
            agentId = request.AgentId,
            message = request.Message
        });

        return new SendInputResponse { Accepted = true };
    }

    public async Task<WaitAgentResponse> WaitAsync(
        WaitAgentRequest request, CancellationToken ct = default)
    {
        var timeoutMs = Math.Clamp(
            request.TimeoutMs ?? _config.DefaultTimeoutMs,
            _config.MinTimeoutMs,
            _config.MaxTimeoutMs);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var results = new Dictionary<string, CollabAgentState>();

        var tasks = request.AgentIds.Select(async agentId =>
        {
            if (!_agents.TryGetValue(agentId, out var runtime))
            {
                return (agentId, new CollabAgentState
                {
                    Status = CollabAgentStatus.NotFound
                });
            }

            if (IsFinalStatus(runtime.Agent.Status))
            {
                return (agentId, new CollabAgentState
                {
                    Status = runtime.Agent.Status,
                    Message = runtime.Agent.CompletionMessage
                });
            }

            var tcs = new TaskCompletionSource<CollabAgentState>();
            runtime.Waiters.Add(tcs);

            try
            {
                var state = await tcs.Task.WaitAsync(cts.Token);
                return (agentId, state);
            }
            catch (OperationCanceledException)
            {
                return (agentId, new CollabAgentState
                {
                    Status = runtime.Agent.Status,
                    Message = "Timeout waiting for agent"
                });
            }
        }).ToList();

        var completed = await Task.WhenAll(tasks);
        foreach (var (agentId, state) in completed)
        {
            results[agentId] = state;
        }

        return new WaitAgentResponse { Results = results };
    }

    public async Task<CloseAgentResponse> CloseAsync(
        CloseAgentRequest request, CancellationToken ct = default)
    {
        var runtime = GetRuntime(request.AgentId);

        await _backend.AbortAsync(runtime.Agent.SessionId, ct);
        await _backend.DisposeSessionAsync(runtime.Agent.SessionId, ct);

        runtime.Agent = runtime.Agent with { Status = CollabAgentStatus.Shutdown };
        runtime.EventSubscription?.Dispose();

        ResolveWaiters(runtime, CollabAgentStatus.Shutdown);

        return new CloseAgentResponse { Status = CollabAgentStatus.Shutdown };
    }

    public async Task<ResumeAgentResponse> ResumeAsync(
        ResumeAgentRequest request, CancellationToken ct = default)
    {
        var runtime = GetRuntime(request.AgentId);

        var session = await _backend.ResumeSessionAsync(runtime.Agent.SessionId, ct);

        runtime.Agent = runtime.Agent with { Status = CollabAgentStatus.Running };
        runtime.EventSubscription = _backend.OnEvent(session.SessionId, evt =>
        {
            HandleAgentEvent(request.AgentId, evt);
        });

        return new ResumeAgentResponse { Agent = runtime.Agent };
    }

    private void HandleAgentEvent(string agentId, BackendEvent evt)
    {
        if (!_agents.TryGetValue(agentId, out var runtime)) return;

        switch (evt.Type)
        {
            case BackendEventType.TextDelta:
                runtime.AssistantText += evt.Text;
                break;

            case BackendEventType.MessageEnd:
                runtime.Agent = runtime.Agent with
                {
                    Status = CollabAgentStatus.Completed,
                    CompletionMessage = runtime.AssistantText
                };
                ResolveWaiters(runtime, CollabAgentStatus.Completed, runtime.AssistantText);
                break;

            case BackendEventType.Error:
                runtime.Agent = runtime.Agent with
                {
                    Status = CollabAgentStatus.Errored,
                    CompletionMessage = evt.Error
                };
                ResolveWaiters(runtime, CollabAgentStatus.Errored, evt.Error);
                break;
        }
    }

    private void ResolveWaiters(CollabAgentRuntime runtime, CollabAgentStatus status, string? message = null)
    {
        var state = new CollabAgentState { Status = status, Message = message };
        foreach (var waiter in runtime.Waiters)
        {
            waiter.TrySetResult(state);
        }
        runtime.Waiters.Clear();
    }

    private CollabAgentRuntime GetRuntime(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var runtime))
            throw new KeyNotFoundException($"Agent {agentId} not found");
        return runtime;
    }

    private static bool IsFinalStatus(CollabAgentStatus status) =>
        status is CollabAgentStatus.Completed
            or CollabAgentStatus.Errored
            or CollabAgentStatus.Shutdown;

    private void EmitToolItem(string method, object data)
    {
        _notificationSink(new TurnNotification { Method = method, Params = data });
    }
}
