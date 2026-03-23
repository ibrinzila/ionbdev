using System.Text.Json;

namespace Codapter.Core.Utilities;

/// <summary>
/// NDJSON (Newline Delimited JSON) utilities.
/// Port of packages/core/src/ndjson.ts
/// </summary>
public static class NdJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, Options) + "\n";
    }

    public static T? Deserialize<T>(string line)
    {
        var trimmed = line.TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(trimmed))
            return default;
        return JsonSerializer.Deserialize<T>(trimmed, Options);
    }

    public static JsonElement? ParseLine(string line)
    {
        var trimmed = line.TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return JsonSerializer.Deserialize<JsonElement>(trimmed);
    }

    public static string SerializeLine(object value)
    {
        return JsonSerializer.Serialize(value, Options) + "\n";
    }
}
