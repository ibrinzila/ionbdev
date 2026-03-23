using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codapter.Core.Models;

/// <summary>
/// JSON-RPC 2.0 protocol types and helpers.
/// Port of packages/core/src/jsonrpc.ts
/// </summary>

public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public JsonRpcId Id { get; init; } = default!;

    [JsonPropertyName("method")]
    public string Method { get; init; } = default!;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; init; }
}

public record JsonRpcNotification
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; init; } = default!;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; init; }
}

public record JsonRpcSuccessResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public JsonRpcId Id { get; init; } = default!;

    [JsonPropertyName("result")]
    public JsonElement Result { get; init; }
}

public record JsonRpcErrorResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public JsonRpcId? Id { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError Error { get; init; } = default!;
}

public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}

/// <summary>
/// Represents a JSON-RPC ID which can be a string, number, or null.
/// </summary>
[JsonConverter(typeof(JsonRpcIdConverter))]
public readonly struct JsonRpcId : IEquatable<JsonRpcId>
{
    private readonly object? _value;

    public JsonRpcId(string value) => _value = value;
    public JsonRpcId(long value) => _value = value;

    public bool IsString => _value is string;
    public bool IsNumber => _value is long;
    public bool IsNull => _value is null;

    public string? AsString => _value as string;
    public long? AsNumber => _value is long l ? l : null;

    public bool Equals(JsonRpcId other) => Equals(_value, other._value);
    public override bool Equals(object? obj) => obj is JsonRpcId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value?.ToString() ?? "null";

    public static bool operator ==(JsonRpcId left, JsonRpcId right) => left.Equals(right);
    public static bool operator !=(JsonRpcId left, JsonRpcId right) => !left.Equals(right);

    public static implicit operator JsonRpcId(string value) => new(value);
    public static implicit operator JsonRpcId(long value) => new(value);
}

public class JsonRpcIdConverter : JsonConverter<JsonRpcId>
{
    public override JsonRpcId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => new JsonRpcId(reader.GetString()!),
            JsonTokenType.Number => new JsonRpcId(reader.GetInt64()),
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Unexpected token type for JsonRpcId: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, JsonRpcId value, JsonSerializerOptions options)
    {
        if (value.IsString) writer.WriteStringValue(value.AsString);
        else if (value.IsNumber) writer.WriteNumberValue(value.AsNumber!.Value);
        else writer.WriteNullValue();
    }
}

/// <summary>
/// Standard JSON-RPC error codes.
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int NotInitialized = -32002;
    public const int AlreadyInitialized = -32001;
}

/// <summary>
/// Helper methods for creating JSON-RPC responses.
/// </summary>
public static class JsonRpcHelpers
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonRpcSuccessResponse Success(JsonRpcId id, object result) => new()
    {
        Id = id,
        Result = JsonSerializer.SerializeToElement(result, SerializerOptions)
    };

    public static JsonRpcErrorResponse Failure(JsonRpcId id, int code, string message, object? data = null) => new()
    {
        Id = id,
        Error = new JsonRpcError { Code = code, Message = message, Data = data }
    };

    public static bool IsRequest(JsonElement element) =>
        element.TryGetProperty("id", out _) && element.TryGetProperty("method", out _);

    public static bool IsNotification(JsonElement element) =>
        !element.TryGetProperty("id", out _) && element.TryGetProperty("method", out _);

    public static bool IsResponse(JsonElement element) =>
        element.TryGetProperty("id", out _) &&
        (element.TryGetProperty("result", out _) || element.TryGetProperty("error", out _));
}
