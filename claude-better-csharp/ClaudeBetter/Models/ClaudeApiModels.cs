using System.Text.Json.Serialization;

namespace ClaudeBetter.Models;

public class ClaudeRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("messages")]
    public List<ClaudeMessageParam> Messages { get; set; } = new();
}

public class ClaudeMessageParam
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ClaudeResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("content")]
    public List<ClaudeContentBlock>? Content { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }
}

public class ClaudeContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

// Streaming event types
public class StreamEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("delta")]
    public StreamDelta? Delta { get; set; }

    [JsonPropertyName("content_block")]
    public ClaudeContentBlock? ContentBlock { get; set; }

    [JsonPropertyName("message")]
    public ClaudeResponse? Message { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }
}

public class StreamDelta
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}
