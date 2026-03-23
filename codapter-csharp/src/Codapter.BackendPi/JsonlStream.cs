using System.Text;
using System.Text.Json;

namespace Codapter.BackendPi;

/// <summary>
/// JSONL stream processing for Pi process communication.
/// Port of packages/backend-pi/src/jsonl.ts
/// </summary>
public static class JsonlStream
{
    public static string SerializeJsonLine(object value)
    {
        return JsonSerializer.Serialize(value) + "\n";
    }

    public static JsonElement ParseJsonLine(string line)
    {
        var trimmed = line.TrimEnd('\r');
        return JsonSerializer.Deserialize<JsonElement>(trimmed);
    }

    /// <summary>
    /// Reads JSONL lines from a StreamReader, invoking the callback for each complete line.
    /// </summary>
    public static async Task ReadLinesAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken ct = default)
    {
        var buffer = new StringBuilder();
        var charBuffer = new char[4096];

        while (!ct.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(charBuffer, ct);
            if (read == 0) break;

            buffer.Append(charBuffer, 0, read);

            while (true)
            {
                var str = buffer.ToString();
                var newlineIdx = str.IndexOf('\n');
                if (newlineIdx < 0) break;

                var line = str[..newlineIdx];
                buffer.Remove(0, newlineIdx + 1);

                var trimmed = line.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmed))
                {
                    onLine(trimmed);
                }
            }
        }

        // Flush remaining
        if (buffer.Length > 0)
        {
            var remaining = buffer.ToString().TrimEnd('\r', '\n');
            if (!string.IsNullOrEmpty(remaining))
            {
                onLine(remaining);
            }
        }
    }
}
