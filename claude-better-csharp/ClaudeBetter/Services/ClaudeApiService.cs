using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ClaudeBetter.Models;

namespace ClaudeBetter.Services;

public class ClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeApiService> _logger;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public ClaudeApiService(HttpClient httpClient, IConfiguration configuration, ILogger<ClaudeApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-api-key", GetApiKey());
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    private string GetApiKey()
    {
        return _configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException(
                "Anthropic API key not found. Set 'Anthropic:ApiKey' in appsettings.json or ANTHROPIC_API_KEY environment variable.");
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        List<ChatMessage> conversationHistory,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var request = new ClaudeRequest
        {
            Stream = true,
            System = systemPrompt,
            Messages = conversationHistory
                .Where(m => m.Role is "user" or "assistant")
                .Select(m => new ClaudeMessageParam
                {
                    Role = m.Role,
                    Content = m.Content
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("TTFB: {Elapsed}ms", sw.ElapsedMilliseconds);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var data = line["data: ".Length..];

            if (data == "[DONE]")
                break;

            StreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<StreamEvent>(data);
            }
            catch
            {
                continue;
            }

            if (evt?.Type == "content_block_delta" && evt.Delta?.Text is not null)
            {
                yield return evt.Delta.Text;
            }
        }

        _logger.LogInformation("Stream completed in {Elapsed}ms", sw.ElapsedMilliseconds);
    }
}
