using System.Text.Json.Serialization;

namespace ClawTeam.Web.Models;

/// <summary>A single cost event recorded by an agent.</summary>
public class CostEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; set; }

    [JsonPropertyName("cost_cents")]
    public double CostCents { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
}

/// <summary>Aggregated cost summary for a team.</summary>
public class CostSummary
{
    [JsonPropertyName("totalCostCents")]
    public double TotalCostCents { get; set; }

    [JsonPropertyName("totalInputTokens")]
    public long TotalInputTokens { get; set; }

    [JsonPropertyName("totalOutputTokens")]
    public long TotalOutputTokens { get; set; }

    [JsonPropertyName("eventCount")]
    public int EventCount { get; set; }

    [JsonPropertyName("byAgent")]
    public Dictionary<string, AgentCostSummary> ByAgent { get; set; } = new();
}

public class AgentCostSummary
{
    [JsonPropertyName("costCents")]
    public double CostCents { get; set; }

    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; set; }

    [JsonPropertyName("eventCount")]
    public int EventCount { get; set; }
}
