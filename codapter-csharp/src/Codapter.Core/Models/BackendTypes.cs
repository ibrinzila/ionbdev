using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codapter.Core.Models;

/// <summary>
/// Backend interface types and event definitions.
/// Port of packages/core/src/backend.ts
/// </summary>

#region Backend Events

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackendEventType
{
    TextDelta,
    ThinkingDelta,
    ToolStart,
    ToolUpdate,
    ToolEnd,
    MessageEnd,
    Error,
    Elicitation,
    TokenUsage
}

public record BackendEvent
{
    [JsonPropertyName("type")]
    public BackendEventType Type { get; init; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = default!;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("toolCallId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("toolName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; init; }

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BackendTokenUsage? Usage { get; init; }
}

public record BackendTokenUsage
{
    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public long CacheReadTokens { get; init; }

    [JsonPropertyName("cacheWriteTokens")]
    public long CacheWriteTokens { get; init; }

    [JsonPropertyName("contextWindow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ContextWindow { get; init; }
}

#endregion

#region Backend Session

public record BackendSessionInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = default!;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public record BackendMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = default!;

    [JsonPropertyName("text")]
    public string Text { get; init; } = default!;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

public record BackendCapabilities
{
    [JsonPropertyName("supportsImages")]
    public bool SupportsImages { get; init; }

    [JsonPropertyName("supportsParallelTools")]
    public bool SupportsParallelTools { get; init; }

    [JsonPropertyName("supportsReasoning")]
    public bool SupportsReasoning { get; init; }
}

#endregion

/// <summary>
/// The IBackend contract defines the backend service interface.
/// Port of the IBackend TypeScript interface.
/// </summary>
public interface IBackend : IAsyncDisposable
{
    Task<BackendSessionInfo> CreateSessionAsync(
        string? model = null,
        string? path = null,
        CancellationToken ct = default);

    Task<BackendSessionInfo> ResumeSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<BackendSessionInfo> ForkSessionAsync(
        string sourceSessionId,
        int? turnIndex = null,
        CancellationToken ct = default);

    Task DisposeSessionAsync(string sessionId, CancellationToken ct = default);

    Task<List<BackendMessage>> GetHistoryAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<BackendSessionInfo> GetSessionInfoAsync(
        string sessionId,
        CancellationToken ct = default);

    Task PromptAsync(
        string sessionId,
        string text,
        List<ImageInput>? images = null,
        CancellationToken ct = default);

    Task AbortAsync(string sessionId, CancellationToken ct = default);

    Task RespondToElicitationAsync(
        string sessionId,
        string elicitationId,
        JsonElement response,
        CancellationToken ct = default);

    Task<ModelListResponse> ListModelsAsync(CancellationToken ct = default);

    Task<ModelInfo> GetCurrentModelAsync(
        string sessionId,
        CancellationToken ct = default);

    Task SetModelAsync(
        string sessionId,
        string modelId,
        CancellationToken ct = default);

    Task<BackendCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    IDisposable OnEvent(string sessionId, Action<BackendEvent> handler);
}
