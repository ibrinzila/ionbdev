using Water.Core;

namespace Water.Agents;

/// <summary>
/// Default input schema for agent tasks.
/// </summary>
public class AgentInput
{
    public string Prompt { get; set; } = "";
}

/// <summary>
/// Default output schema for agent tasks.
/// </summary>
public class AgentOutput
{
    public string Response { get; set; } = "";
}

/// <summary>
/// Abstract base class for LLM providers.
/// </summary>
public interface ILlmProvider
{
    Task<Dictionary<string, object?>> CompleteAsync(List<Dictionary<string, string>> messages, Dictionary<string, object?>? options = null);
}

/// <summary>
/// Mock provider for testing. Returns canned responses.
/// </summary>
public class MockProvider : ILlmProvider
{
    private readonly Queue<string> _responses;
    private readonly string _defaultResponse;

    public MockProvider(IEnumerable<string>? responses = null, string defaultResponse = "Mock response")
    {
        _responses = responses != null ? new Queue<string>(responses) : new Queue<string>();
        _defaultResponse = defaultResponse;
    }

    public Task<Dictionary<string, object?>> CompleteAsync(List<Dictionary<string, string>> messages, Dictionary<string, object?>? options = null)
    {
        var response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        return Task.FromResult<Dictionary<string, object?>>(new()
        {
            ["text"] = response,
            ["usage"] = new Dictionary<string, object?> { ["input_tokens"] = 10, ["output_tokens"] = 10 }
        });
    }
}

/// <summary>
/// OpenAI-compatible provider (requires HttpClient configuration).
/// </summary>
public class OpenAIProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenAIProvider(string? apiKey = null, string model = "gpt-4", HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        _model = model;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Dictionary<string, object?>> CompleteAsync(List<Dictionary<string, string>> messages, Dictionary<string, object?>? options = null)
    {
        var request = new
        {
            model = _model,
            messages = messages,
            temperature = options?.TryGetValue("temperature", out var t) == true ? t : 0.7,
            max_tokens = options?.TryGetValue("max_tokens", out var mt) == true ? mt : 1024
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseJson);

        var text = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return new Dictionary<string, object?>
        {
            ["text"] = text,
            ["usage"] = new Dictionary<string, object?>
            {
                ["input_tokens"] = result.GetProperty("usage").GetProperty("prompt_tokens").GetInt32(),
                ["output_tokens"] = result.GetProperty("usage").GetProperty("completion_tokens").GetInt32()
            }
        };
    }
}

/// <summary>
/// Anthropic Claude provider.
/// </summary>
public class AnthropicProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;

    public AnthropicProvider(string? apiKey = null, string model = "claude-sonnet-4-20250514", HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        _model = model;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Dictionary<string, object?>> CompleteAsync(List<Dictionary<string, string>> messages, Dictionary<string, object?>? options = null)
    {
        var request = new
        {
            model = _model,
            messages = messages,
            max_tokens = options?.TryGetValue("max_tokens", out var mt) == true ? mt : 1024
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseJson);

        var text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        return new Dictionary<string, object?>
        {
            ["text"] = text,
            ["usage"] = new Dictionary<string, object?>
            {
                ["input_tokens"] = result.GetProperty("usage").GetProperty("input_tokens").GetInt32(),
                ["output_tokens"] = result.GetProperty("usage").GetProperty("output_tokens").GetInt32()
            }
        };
    }
}

/// <summary>
/// Custom provider wrapping a user-supplied function.
/// </summary>
public class CustomProvider : ILlmProvider
{
    private readonly Func<List<Dictionary<string, string>>, Dictionary<string, object?>?, Task<Dictionary<string, object?>>> _completeFn;

    public CustomProvider(Func<List<Dictionary<string, string>>, Dictionary<string, object?>?, Task<Dictionary<string, object?>>> completeFn)
    {
        _completeFn = completeFn;
    }

    public Task<Dictionary<string, object?>> CompleteAsync(List<Dictionary<string, string>> messages, Dictionary<string, object?>? options = null)
        => _completeFn(messages, options);
}

/// <summary>
/// Factory for creating agent tasks that wrap LLM calls.
/// </summary>
public static class AgentTaskFactory
{
    public static WaterTask Create(
        ILlmProvider provider,
        string systemPrompt = "",
        string? id = null,
        string? description = null,
        double temperature = 0.7,
        int maxTokens = 1024)
    {
        return new WaterTask(
            execute: async (parameters, context) =>
            {
                var inputData = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;

                var prompt = inputData.TryGetValue("prompt", out var p) ? p?.ToString() ?? "" : "";

                var messages = new List<Dictionary<string, string>>();
                if (!string.IsNullOrEmpty(systemPrompt))
                    messages.Add(new() { ["role"] = "system", ["content"] = systemPrompt });
                messages.Add(new() { ["role"] = "user", ["content"] = prompt });

                var result = await provider.CompleteAsync(messages, new Dictionary<string, object?>
                {
                    ["temperature"] = temperature,
                    ["max_tokens"] = maxTokens
                });

                return new Dictionary<string, object?>
                {
                    ["response"] = result.TryGetValue("text", out var text) ? text : "",
                    ["usage"] = result.TryGetValue("usage", out var usage) ? usage : null
                };
            },
            id: id ?? $"agent_{Guid.NewGuid().ToString("N")[..8]}",
            description: description ?? "Agent task");
    }
}
