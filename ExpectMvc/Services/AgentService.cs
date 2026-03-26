using System.Text.Json;
using System.Text;
using ExpectMvc.Models;

namespace ExpectMvc.Services;

public class AgentService : IAgentService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IConfiguration config, ILogger<AgentService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<TestPlan> GeneratePlanAsync(GitDiff diff, AgentProvider provider, string? message = null)
    {
        _logger.LogInformation("Generating test plan with {Provider}", provider);

        var prompt = BuildPrompt(diff, message);

        var steps = provider switch
        {
            AgentProvider.Claude => await GenerateWithClaudeAsync(prompt),
            AgentProvider.Codex => await GenerateWithCodexAsync(prompt),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        return new TestPlan
        {
            Description = message ?? $"Auto-generated plan for {diff.Files.Count} changed file(s)",
            Steps = steps,
            SourceDiff = diff,
            Agent = provider,
            Target = TestTarget.Changes
        };
    }

    private static string BuildPrompt(GitDiff diff, string? message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a QA engineer. Analyze the following code changes and generate a test plan.");
        sb.AppendLine("Return a JSON array of test steps.");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(message))
        {
            sb.AppendLine($"User instruction: {message}");
            sb.AppendLine();
        }

        sb.AppendLine($"Branch: {diff.Branch}");
        sb.AppendLine($"Files changed: {diff.Files.Count}");
        sb.AppendLine();

        foreach (var file in diff.Files)
        {
            sb.AppendLine($"--- {file.Path} ({file.Type}) +{file.Additions} -{file.Deletions} ---");
            if (!string.IsNullOrEmpty(file.Patch))
            {
                sb.AppendLine(file.Patch);
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Respond with ONLY a JSON array of objects, each with:");
        sb.AppendLine("  { \"order\": number, \"action\": string, \"selector\": string|null, \"value\": string|null, \"expected\": string }");

        return sb.ToString();
    }

    private async Task<List<TestStep>> GenerateWithClaudeAsync(string prompt)
    {
        var apiKey = _config["Expect:AnthropicApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "AnthropicApiKey is not configured. Set Expect:AnthropicApiKey in appsettings.json or environment.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return ParseStepsFromResponse(json);
    }

    private async Task<List<TestStep>> GenerateWithCodexAsync(string prompt)
    {
        var apiKey = _config["Expect:OpenAiApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "OpenAiApiKey is not configured. Set Expect:OpenAiApiKey in appsettings.json or environment.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var body = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 4096
        };

        var response = await http.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return ParseStepsFromResponse(json);
    }

    private static List<TestStep> ParseStepsFromResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Extract text content from either Anthropic or OpenAI response format
        var text = "";
        if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            // Anthropic format
            text = content[0].GetProperty("text").GetString() ?? "";
        }
        else if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            // OpenAI format
            text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        // Extract JSON array from response (may be wrapped in markdown code fences)
        var jsonStart = text.IndexOf('[');
        var jsonEnd = text.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0)
            return new List<TestStep>();

        var stepsJson = text[jsonStart..(jsonEnd + 1)];
        var steps = JsonSerializer.Deserialize<List<TestStep>>(stepsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return steps ?? new List<TestStep>();
    }
}
