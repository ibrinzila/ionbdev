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
    [JsonPropertyName("total_cost_cents")]
    public double TotalCostCents { get; set; }

    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; set; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; set; }

    [JsonPropertyName("event_count")]
    public int EventCount { get; set; }

    [JsonPropertyName("by_agent")]
    public Dictionary<string, AgentCostSummary> ByAgent { get; set; } = new();
}

public class AgentCostSummary
{
    [JsonPropertyName("cost_cents")]
    public double CostCents { get; set; }

    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; set; }

    [JsonPropertyName("event_count")]
    public int EventCount { get; set; }
}
