using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codapter.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codapter.BackendPi;

/// <summary>
/// Manages communication with a Pi coding agent subprocess via JSON-RPC over stdio.
/// Port of packages/backend-pi/src/pi-process.ts
/// </summary>

internal record PiRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("method")]
    public string Method { get; init; } = default!;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

internal record PiRpcResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Error { get; init; }

    [JsonPropertyName("method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Method { get; init; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; init; }
}

public class PiProcessSession : IAsyncDisposable
{
    private readonly string _command;
    private readonly string[] _args;
    private readonly string? _workingDirectory;
    private readonly Dictionary<string, string>? _env;
    private readonly ILogger<PiProcessSession>? _logger;
    private readonly string? _debugLogFile;

    private Process? _process;
    private CancellationTokenSource? _readCts;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly List<Action<BackendEvent>> _eventListeners = new();
    private readonly object _listenerLock = new();

    private string? _sessionId;
    private string? _sessionFile;
    private string? _modelId;

    public string? SessionId => _sessionId;

    public PiProcessSession(
        string command = "npx",
        string[]? args = null,
        string? workingDirectory = null,
        Dictionary<string, string>? env = null,
        ILogger<PiProcessSession>? logger = null,
        string? debugLogFile = null)
    {
        _command = command;
        _args = args ?? new[] { "@mariozechner/pi-coding-agent" };
        _workingDirectory = workingDirectory;
        _env = env;
        _logger = logger;
        _debugLogFile = debugLogFile;
    }

    public async Task<string> StartFreshAsync(
        string? model = null, string? path = null, CancellationToken ct = default)
    {
        StartProcess(path);

        var sessionId = Guid.NewGuid().ToString();
        var result = await SendRequestAsync("session/create", new
        {
            sessionId,
            model,
            path
        }, ct);

        _sessionId = sessionId;
        if (result.TryGetProperty("sessionFile", out var sf))
            _sessionFile = sf.GetString();
        _modelId = model;

        return sessionId;
    }

    public async Task AttachSessionAsync(
        string sessionId, string sessionFile, CancellationToken ct = default)
    {
        StartProcess();

        var result = await SendRequestAsync("session/attach", new
        {
            sessionId,
            sessionFile
        }, ct);

        _sessionId = sessionId;
        _sessionFile = sessionFile;
    }

    public async Task<string> ForkSessionAsync(
        string sourceSessionId, int? turnIndex = null, CancellationToken ct = default)
    {
        var newSessionId = Guid.NewGuid().ToString();

        await SendRequestAsync("session/fork", new
        {
            sourceSessionId,
            newSessionId,
            turnIndex
        }, ct);

        _sessionId = newSessionId;
        return newSessionId;
    }

    public async Task PromptAsync(
        string text, List<ImageInput>? images = null, CancellationToken ct = default)
    {
        var imageData = images?.Select(img => new
        {
            base64 = img.Base64 ?? ConvertFileToBase64(img.FilePath),
            mimeType = img.MimeType ?? "image/png"
        }).ToList();

        await SendRequestAsync("prompt", new
        {
            sessionId = _sessionId,
            text,
            images = imageData
        }, ct);
    }

    public async Task AbortAsync(CancellationToken ct = default)
    {
        await SendRequestAsync("abort", new { sessionId = _sessionId }, ct);
    }

    public async Task RespondToElicitationAsync(
        string elicitationId, JsonElement response, CancellationToken ct = default)
    {
        await SendRequestAsync("elicitation/respond", new
        {
            sessionId = _sessionId,
            elicitationId,
            response
        }, ct);
    }

    public async Task<List<BackendMessage>> GetMessagesAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("session/messages", new
        {
            sessionId = _sessionId
        }, ct);

        return result.TryGetProperty("messages", out var msgs)
            ? JsonSerializer.Deserialize<List<BackendMessage>>(msgs.GetRawText()) ?? new()
            : new();
    }

    public async Task<JsonElement> GetSessionStatsAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync("session/stats", new
        {
            sessionId = _sessionId
        }, ct);
    }

    public async Task SetModelAsync(string modelId, CancellationToken ct = default)
    {
        await SendRequestAsync("model/set", new
        {
            sessionId = _sessionId,
            modelId
        }, ct);
        _modelId = modelId;
    }

    public async Task<List<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("model/list", null, ct);

        if (result.TryGetProperty("models", out var models))
        {
            return JsonSerializer.Deserialize<List<ModelInfo>>(models.GetRawText()) ?? new();
        }

        return new();
    }

    public IDisposable OnEvent(Action<BackendEvent> handler)
    {
        lock (_listenerLock)
        {
            _eventListeners.Add(handler);
        }

        return new EventUnsubscriber(() =>
        {
            lock (_listenerLock)
            {
                _eventListeners.Remove(handler);
            }
        });
    }

    private void StartProcess(string? workingDir = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _command,
            Arguments = string.Join(' ', _args),
            WorkingDirectory = workingDir ?? _workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (_env is not null)
        {
            foreach (var (key, value) in _env)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadOutputLoop(_readCts.Token));

        _process.Exited += (_, _) =>
        {
            // Reject all pending requests
            foreach (var (id, tcs) in _pendingRequests)
            {
                tcs.TrySetException(new InvalidOperationException("Pi process exited"));
                _pendingRequests.TryRemove(id, out _);
            }
        };

        DebugLog("startup", new { command = _command, args = _args });
    }

    private async Task ReadOutputLoop(CancellationToken ct)
    {
        try
        {
            await JsonlStream.ReadLinesAsync(_process!.StandardOutput, line =>
            {
                DebugLog("stdout", line);

                try
                {
                    var msg = JsonSerializer.Deserialize<PiRpcResponse>(line);
                    if (msg == null) return;

                    // Response to a request
                    if (msg.Id != null && _pendingRequests.TryRemove(msg.Id, out var tcs))
                    {
                        if (msg.Error.HasValue)
                            tcs.TrySetException(new InvalidOperationException(msg.Error.Value.ToString()));
                        else
                            tcs.TrySetResult(msg.Result ?? default);
                        return;
                    }

                    // Notification (event)
                    if (msg.Method != null)
                    {
                        var backendEvent = ParseBackendEvent(msg.Method, msg.Params);
                        if (backendEvent != null)
                        {
                            EmitEvent(backendEvent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse Pi output: {Line}", line);
                }
            }, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task<JsonElement> SendRequestAsync(
        string method, object? @params, CancellationToken ct)
    {
        if (_process is null || _process.HasExited)
            throw new InvalidOperationException("Pi process is not running");

        var id = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingRequests[id] = tcs;

        var request = new PiRpcRequest
        {
            Id = id,
            Method = method,
            Params = @params
        };

        var line = JsonlStream.SerializeJsonLine(request);
        DebugLog("stdin", line.TrimEnd());

        await _process.StandardInput.WriteAsync(line.AsMemory(), ct);
        await _process.StandardInput.FlushAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(id, out _);
            throw;
        }
    }

    private static BackendEvent? ParseBackendEvent(string method, JsonElement? @params)
    {
        if (@params == null) return null;

        var p = @params.Value;

        return method switch
        {
            "event/textDelta" => new BackendEvent
            {
                Type = BackendEventType.TextDelta,
                SessionId = GetString(p, "sessionId") ?? "",
                Text = GetString(p, "text")
            },
            "event/thinkingDelta" => new BackendEvent
            {
                Type = BackendEventType.ThinkingDelta,
                SessionId = GetString(p, "sessionId") ?? "",
                Text = GetString(p, "text")
            },
            "event/toolStart" => new BackendEvent
            {
                Type = BackendEventType.ToolStart,
                SessionId = GetString(p, "sessionId") ?? "",
                ToolCallId = GetString(p, "toolCallId"),
                ToolName = GetString(p, "toolName"),
                Input = p.TryGetProperty("input", out var inp) ? inp : null
            },
            "event/toolUpdate" => new BackendEvent
            {
                Type = BackendEventType.ToolUpdate,
                SessionId = GetString(p, "sessionId") ?? "",
                ToolCallId = GetString(p, "toolCallId"),
                Output = p.TryGetProperty("output", out var outp) ? outp : null
            },
            "event/toolEnd" => new BackendEvent
            {
                Type = BackendEventType.ToolEnd,
                SessionId = GetString(p, "sessionId") ?? "",
                ToolCallId = GetString(p, "toolCallId"),
                Output = p.TryGetProperty("output", out var outEnd) ? outEnd : null
            },
            "event/messageEnd" => new BackendEvent
            {
                Type = BackendEventType.MessageEnd,
                SessionId = GetString(p, "sessionId") ?? ""
            },
            "event/error" => new BackendEvent
            {
                Type = BackendEventType.Error,
                SessionId = GetString(p, "sessionId") ?? "",
                Error = GetString(p, "error")
            },
            "event/tokenUsage" => new BackendEvent
            {
                Type = BackendEventType.TokenUsage,
                SessionId = GetString(p, "sessionId") ?? "",
                Usage = p.TryGetProperty("usage", out var usage)
                    ? JsonSerializer.Deserialize<BackendTokenUsage>(usage.GetRawText())
                    : null
            },
            _ => null
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var val) ? val.GetString() : null;
    }

    private void EmitEvent(BackendEvent evt)
    {
        List<Action<BackendEvent>> listeners;
        lock (_listenerLock)
        {
            listeners = new(_eventListeners);
        }

        foreach (var listener in listeners)
        {
            try
            {
                listener(evt);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in event listener");
            }
        }
    }

    private void DebugLog(string label, object data)
    {
        if (_debugLogFile == null) return;

        try
        {
            var entry = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow,
                label,
                data
            });
            File.AppendAllText(_debugLogFile, entry + "\n");
        }
        catch
        {
            // Best-effort debug logging
        }
    }

    private static string? ConvertFileToBase64(string? filePath)
    {
        if (filePath == null || !File.Exists(filePath))
            return null;
        return Convert.ToBase64String(File.ReadAllBytes(filePath));
    }

    public async ValueTask DisposeAsync()
    {
        _readCts?.Cancel();

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already exited
            }

            try
            {
                await _process.WaitForExitAsync(new CancellationTokenSource(5000).Token);
            }
            catch
            {
                // Timeout
            }

            _process.Dispose();
        }

        _readCts?.Dispose();
    }

    private class EventUnsubscriber : IDisposable
    {
        private readonly Action _unsubscribe;
        public EventUnsubscriber(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
