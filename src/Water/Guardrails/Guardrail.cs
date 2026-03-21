using Water.Core;

namespace Water.Guardrails;

public enum GuardrailAction
{
    Block,
    Warn,
    Retry,
    Fallback
}

/// <summary>
/// Result of a guardrail validation check.
/// </summary>
public class GuardrailResult
{
    public bool Passed { get; set; }
    public string Reason { get; set; } = "";
    public Dictionary<string, object?> Details { get; set; } = new();
    public string GuardrailName { get; set; } = "";

    public static implicit operator bool(GuardrailResult result) => result.Passed;
}

/// <summary>
/// Raised when a guardrail check fails with action=Block.
/// </summary>
public class GuardrailViolationException : WaterException
{
    public GuardrailResult Result { get; }

    public GuardrailViolationException(GuardrailResult result)
        : base($"GuardrailViolation: {result.GuardrailName} - {result.Reason}")
    {
        Result = result;
    }
}

/// <summary>
/// Base class for input/output validation rules.
/// </summary>
public abstract class Guardrail
{
    public string Name { get; set; } = "guardrail";
    public GuardrailAction Action { get; set; } = GuardrailAction.Block;

    protected Guardrail(string? name = null, GuardrailAction action = GuardrailAction.Block)
    {
        if (name != null) Name = name;
        Action = action;
    }

    public abstract GuardrailResult Validate(Dictionary<string, object?> data, ExecutionContext? context = null);

    public GuardrailResult Check(Dictionary<string, object?> data, ExecutionContext? context = null)
    {
        var result = Validate(data, context);
        result.GuardrailName = Name;

        if (!result.Passed && Action == GuardrailAction.Block)
            throw new GuardrailViolationException(result);

        return result;
    }
}

/// <summary>
/// Compose multiple guardrails in sequence.
/// </summary>
public class GuardrailChain
{
    public List<Guardrail> Guardrails { get; } = new();

    public GuardrailChain(List<Guardrail>? guardrails = null)
    {
        if (guardrails != null) Guardrails.AddRange(guardrails);
    }

    public GuardrailChain Add(Guardrail guardrail)
    {
        Guardrails.Add(guardrail);
        return this;
    }

    public List<GuardrailResult> Check(Dictionary<string, object?> data, ExecutionContext? context = null)
    {
        return Guardrails.Select(g => g.Check(data, context)).ToList();
    }
}

/// <summary>
/// Content filter guardrail - blocks content containing forbidden patterns.
/// </summary>
public class ContentFilter : Guardrail
{
    public List<string> ForbiddenPatterns { get; }

    public ContentFilter(List<string> forbiddenPatterns, string? name = null, GuardrailAction action = GuardrailAction.Block)
        : base(name ?? "content_filter", action)
    {
        ForbiddenPatterns = forbiddenPatterns;
    }

    public override GuardrailResult Validate(Dictionary<string, object?> data, ExecutionContext? context = null)
    {
        var content = System.Text.Json.JsonSerializer.Serialize(data);
        foreach (var pattern in ForbiddenPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return new GuardrailResult { Passed = false, Reason = $"Content contains forbidden pattern: {pattern}" };
        }
        return new GuardrailResult { Passed = true };
    }
}

/// <summary>
/// Cost guardrail - blocks if estimated cost exceeds budget.
/// </summary>
public class CostGuardrail : Guardrail
{
    public double MaxCost { get; }

    public CostGuardrail(double maxCost, string? name = null, GuardrailAction action = GuardrailAction.Block)
        : base(name ?? "cost_guardrail", action)
    {
        MaxCost = maxCost;
    }

    public override GuardrailResult Validate(Dictionary<string, object?> data, ExecutionContext? context = null)
    {
        if (data.TryGetValue("estimated_cost", out var costObj) && costObj is double cost && cost > MaxCost)
        {
            return new GuardrailResult
            {
                Passed = false,
                Reason = $"Estimated cost ${cost:F4} exceeds budget ${MaxCost:F4}",
                Details = new() { ["estimated_cost"] = cost, ["max_cost"] = MaxCost }
            };
        }
        return new GuardrailResult { Passed = true };
    }
}

/// <summary>
/// Topic guardrail - blocks content about forbidden topics.
/// </summary>
public class TopicGuardrail : Guardrail
{
    public List<string> ForbiddenTopics { get; }

    public TopicGuardrail(List<string> forbiddenTopics, string? name = null, GuardrailAction action = GuardrailAction.Block)
        : base(name ?? "topic_guardrail", action)
    {
        ForbiddenTopics = forbiddenTopics;
    }

    public override GuardrailResult Validate(Dictionary<string, object?> data, ExecutionContext? context = null)
    {
        var content = System.Text.Json.JsonSerializer.Serialize(data).ToLowerInvariant();
        foreach (var topic in ForbiddenTopics)
        {
            if (content.Contains(topic.ToLowerInvariant()))
                return new GuardrailResult { Passed = false, Reason = $"Content touches forbidden topic: {topic}" };
        }
        return new GuardrailResult { Passed = true };
    }
}
