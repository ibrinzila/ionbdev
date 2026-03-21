using Water.Core;

namespace Water.Tasks;

/// <summary>
/// Standard task library providing common operations.
/// </summary>
public static class StandardTasks
{
    /// <summary>
    /// HTTP request task.
    /// </summary>
    public static WaterTask HttpRequest(string? id = null)
    {
        return new WaterTask(
            execute: async (parameters, context) =>
            {
                var data = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;

                var url = data.TryGetValue("url", out var u) ? u?.ToString() ?? "" : "";
                var method = data.TryGetValue("method", out var m) ? m?.ToString() ?? "GET" : "GET";

                using var client = new HttpClient();
                var request = new HttpRequestMessage(new HttpMethod(method), url);

                if (data.TryGetValue("body", out var body) && body is string bodyStr)
                    request.Content = new StringContent(bodyStr, System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return new Dictionary<string, object?>
                {
                    ["status_code"] = (int)response.StatusCode,
                    ["body"] = content,
                    ["headers"] = response.Headers.ToDictionary(h => h.Key, h => (object?)string.Join(", ", h.Value))
                };
            },
            id: id ?? "http_request",
            description: "HTTP request task");
    }

    /// <summary>
    /// JSON transform task - applies field mapping.
    /// </summary>
    public static WaterTask JsonTransform(Dictionary<string, string> fieldMapping, string? id = null)
    {
        return TaskFactory.CreateSync(
            (parameters, context) =>
            {
                var data = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;

                var result = new Dictionary<string, object?>();
                foreach (var (target, source) in fieldMapping)
                {
                    if (data.TryGetValue(source, out var value))
                        result[target] = value;
                }
                return result;
            },
            id: id ?? "json_transform",
            description: "JSON transform task");
    }

    /// <summary>
    /// Delay task - pauses for a specified duration.
    /// </summary>
    public static WaterTask Delay(TimeSpan duration, string? id = null)
    {
        return new WaterTask(
            execute: async (parameters, context) =>
            {
                await Task.Delay(duration);
                var data = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;
                return new Dictionary<string, object?>(data);
            },
            id: id ?? "delay",
            description: $"Delay {duration.TotalSeconds}s");
    }

    /// <summary>
    /// Log task - logs data and passes it through.
    /// </summary>
    public static WaterTask Log(string? id = null)
    {
        return TaskFactory.CreateSync(
            (parameters, context) =>
            {
                var data = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;
                Console.WriteLine($"[LOG] Task={context.TaskId} Data={System.Text.Json.JsonSerializer.Serialize(data)}");
                return new Dictionary<string, object?>(data);
            },
            id: id ?? "log_task",
            description: "Log task");
    }

    /// <summary>
    /// No-op task - passes data through unchanged.
    /// </summary>
    public static WaterTask Noop(string? id = null)
    {
        return TaskFactory.CreateSync(
            (parameters, context) =>
            {
                var data = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;
                return new Dictionary<string, object?>(data);
            },
            id: id ?? "noop",
            description: "No-op task");
    }
}
