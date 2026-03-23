using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codapter.Core.Models;

/// <summary>
/// Protocol types for the Codex app-server protocol.
/// Port of packages/core/src/protocol.ts
/// </summary>

#region Initialization

public record ClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("version")]
    public string Version { get; init; } = default!;

    [JsonPropertyName("platform")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Platform { get; init; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; init; }
}

public record InitializeParams
{
    [JsonPropertyName("clientInfo")]
    public ClientInfo ClientInfo { get; init; } = default!;

    [JsonPropertyName("capabilities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Capabilities { get; init; }
}

public record InitializeResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "codapter";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.0.3";

    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; init; } = new();
}

public record ServerCapabilities
{
    [JsonPropertyName("supportsCollaboration")]
    public bool SupportsCollaboration { get; init; }

    [JsonPropertyName("supportsMcp")]
    public bool SupportsMcp { get; init; }
}

#endregion

#region Authentication

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthMode
{
    ApiKey,
    ChatGpt
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanType
{
    Free,
    Plus,
    Pro,
    Enterprise,
    Team
}

public record Account
{
    [JsonPropertyName("mode")]
    public AuthMode Mode { get; init; }

    [JsonPropertyName("planType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlanType? PlanType { get; init; }

    [JsonPropertyName("isLoggedIn")]
    public bool IsLoggedIn { get; init; }
}

public record GetAuthStatusResponse
{
    [JsonPropertyName("accounts")]
    public List<Account> Accounts { get; init; } = new();
}

public record LoginAccountParams
{
    [JsonPropertyName("mode")]
    public AuthMode Mode { get; init; }

    [JsonPropertyName("apiKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; init; }

    [JsonPropertyName("chatGptToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChatGptToken { get; init; }
}

#endregion

#region Configuration

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SandboxMode
{
    FullAccess,
    ReadOnly,
    WorkspaceWrite
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReasoningEffort
{
    Low,
    Medium,
    High
}

public record Config
{
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("review_model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewModel { get; init; }

    [JsonPropertyName("sandbox_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SandboxMode? SandboxModeValue { get; init; }

    [JsonPropertyName("web_search")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? WebSearch { get; init; }

    [JsonPropertyName("instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instructions { get; init; }

    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReasoningEffort? ReasoningEffortValue { get; init; }

    [JsonPropertyName("reasoning_summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningSummary { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigLayerSource
{
    Mdm,
    System,
    User,
    Project,
    SessionFlags
}

public record ConfigLayer
{
    [JsonPropertyName("source")]
    public ConfigLayerSource Source { get; init; }

    [JsonPropertyName("config")]
    public Config Config { get; init; } = default!;
}

public record ConfigReadResponse
{
    [JsonPropertyName("config")]
    public Config Config { get; init; } = default!;

    [JsonPropertyName("layers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ConfigLayer>? Layers { get; init; }

    [JsonPropertyName("version")]
    public long Version { get; init; }
}

public record ConfigEdit
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = default!;

    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }

    [JsonPropertyName("strategy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Strategy { get; init; }
}

public record ConfigWriteParams
{
    [JsonPropertyName("version")]
    public long Version { get; init; }

    [JsonPropertyName("edits")]
    public List<ConfigEdit> Edits { get; init; } = new();
}

public record ConfigWriteResponse
{
    [JsonPropertyName("version")]
    public long Version { get; init; }

    [JsonPropertyName("config")]
    public Config Config { get; init; } = default!;
}

#endregion

#region Models

public record ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; init; }

    [JsonPropertyName("supportsImages")]
    public bool SupportsImages { get; init; }

    [JsonPropertyName("supportsReasoning")]
    public bool SupportsReasoning { get; init; }

    [JsonPropertyName("reasoningEffortOptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ReasoningEffortOptions { get; init; }
}

public record ModelListResponse
{
    [JsonPropertyName("models")]
    public List<ModelInfo> Models { get; init; } = new();

    [JsonPropertyName("current")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Current { get; init; }
}

#endregion

#region Threads

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThreadStatus
{
    Active,
    Archived
}

public record ThreadGitInfo
{
    [JsonPropertyName("branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Branch { get; init; }

    [JsonPropertyName("commit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Commit { get; init; }
}

public record Thread
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public ThreadStatus Status { get; init; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    [JsonPropertyName("git")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThreadGitInfo? Git { get; init; }

    [JsonPropertyName("turns")]
    public List<Turn> Turns { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

#endregion

#region Turns & Items

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TurnStatus
{
    InProgress,
    Completed,
    Interrupted,
    Failed
}

public record Turn
{
    [JsonPropertyName("turnId")]
    public string TurnId { get; init; } = default!;

    [JsonPropertyName("status")]
    public TurnStatus Status { get; init; }

    [JsonPropertyName("items")]
    public List<ThreadItem> Items { get; init; } = new();

    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThreadTokenUsage? Usage { get; init; }
}

public record ThreadTokenUsage
{
    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public long CacheReadTokens { get; init; }

    [JsonPropertyName("cacheWriteTokens")]
    public long CacheWriteTokens { get; init; }

    [JsonPropertyName("modelContextWindow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ModelContextWindow { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThreadItemType
{
    UserMessage,
    AgentMessage,
    Reasoning,
    CommandExecution,
    FileChange
}

public record ThreadItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("type")]
    public ThreadItemType Type { get; init; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("summaryText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SummaryText { get; init; }

    [JsonPropertyName("command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; init; }

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Output { get; init; }

    [JsonPropertyName("exitCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; init; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; init; }

    [JsonPropertyName("changeKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChangeKind { get; init; }

    [JsonPropertyName("diff")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diff { get; init; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ItemStatus { get; init; }
}

#endregion

#region User Input

public record UserInput
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ImageInput>? Images { get; init; }
}

public record ImageInput
{
    [JsonPropertyName("base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Base64 { get; init; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; init; }

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }
}

#endregion

#region Thread Operations

public record ThreadStartParams
{
    [JsonPropertyName("input")]
    public UserInput Input { get; init; } = default!;

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }
}

public record ThreadStartResponse
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("turnId")]
    public string TurnId { get; init; } = default!;
}

public record ThreadResumeParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;
}

public record ThreadResumeResponse
{
    [JsonPropertyName("thread")]
    public Thread Thread { get; init; } = default!;
}

public record ThreadForkParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("turnIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TurnIndex { get; init; }
}

public record ThreadForkResponse
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;
}

public record ThreadArchiveParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;
}

public record ThreadListResponse
{
    [JsonPropertyName("threads")]
    public List<Thread> Threads { get; init; } = new();
}

#endregion

#region Turn Operations

public record TurnStartParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("input")]
    public UserInput Input { get; init; } = default!;
}

public record TurnStartResponse
{
    [JsonPropertyName("turnId")]
    public string TurnId { get; init; } = default!;
}

public record TurnInterruptParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;
}

#endregion

#region Command Execution

public record CommandExecParams
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; init; } = default!;

    [JsonPropertyName("command")]
    public string Command { get; init; } = default!;

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Args { get; init; }

    [JsonPropertyName("cwd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cwd { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; init; }

    [JsonPropertyName("timeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Timeout { get; init; }

    [JsonPropertyName("tty")]
    public bool Tty { get; init; }

    [JsonPropertyName("streaming")]
    public bool Streaming { get; init; }

    [JsonPropertyName("processId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessId { get; init; }
}

public record CommandExecResponse
{
    [JsonPropertyName("processId")]
    public string ProcessId { get; init; } = default!;

    [JsonPropertyName("exitCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; init; }

    [JsonPropertyName("stdout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stdout { get; init; }

    [JsonPropertyName("stderr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stderr { get; init; }
}

public record CommandExecStdinParams
{
    [JsonPropertyName("processId")]
    public string ProcessId { get; init; } = default!;

    [JsonPropertyName("data")]
    public string Data { get; init; } = default!;
}

public record CommandExecInterruptParams
{
    [JsonPropertyName("processId")]
    public string ProcessId { get; init; } = default!;
}

#endregion

#region Sandbox

public record SandboxPolicy
{
    [JsonPropertyName("mode")]
    public SandboxMode Mode { get; init; }

    [JsonPropertyName("writePaths")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? WritePaths { get; init; }
}

#endregion

#region Experimental Features

public record ExperimentalFeature
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("staging")]
    public bool Staging { get; init; }
}

#endregion

#region Collaboration Modes

public record CollaborationModeMask
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
}

#endregion

/// <summary>
/// Validates and returns a proper JsonRpcId.
/// </summary>
public static class ProtocolHelpers
{
    public static JsonRpcId AsJsonRpcId(object? value) => value switch
    {
        string s => new JsonRpcId(s),
        int i => new JsonRpcId(i),
        long l => new JsonRpcId(l),
        _ => default
    };
}
