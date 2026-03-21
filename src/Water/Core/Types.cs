namespace Water.Core;

/// <summary>
/// Type aliases and node type definitions for the execution graph.
/// </summary>
public enum NodeType
{
    Sequential,
    Parallel,
    Branch,
    Loop,
    Map,
    Dag,
    TryCatch,
    AgenticLoop
}

/// <summary>
/// Represents a single node in the execution graph.
/// </summary>
public class ExecutionNode
{
    public NodeType Type { get; set; }
    public WaterTask? Task { get; set; }
    public List<WaterTask>? Tasks { get; set; }
    public List<BranchCondition>? Branches { get; set; }
    public Func<Dictionary<string, object?>, bool>? Condition { get; set; }
    public Func<Dictionary<string, object?>, bool>? When { get; set; }
    public WaterTask? Fallback { get; set; }
    public int MaxIterations { get; set; } = 100;
    public string? Over { get; set; }
    public Dictionary<string, List<string>>? Dependencies { get; set; }

    // Agentic loop fields
    public object? Provider { get; set; }
    public object? Tools { get; set; }
    public string SystemPrompt { get; set; } = "";
    public Dictionary<string, object?> Config { get; set; } = new();
}

/// <summary>
/// A branch condition mapping a predicate to a task.
/// </summary>
public class BranchCondition
{
    public required Func<Dictionary<string, object?>, bool> Condition { get; set; }
    public required WaterTask Task { get; set; }
}
