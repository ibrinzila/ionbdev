using Water.Core;

namespace Water.Observability;

/// <summary>
/// Token usage for a single LLM call.
/// </summary>
public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Cost of a single task execution.
/// </summary>
public class TaskCost
{
    public string TaskId { get; set; }
    public TokenUsage Usage { get; set; }
    public double Cost { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public TaskCost(string taskId, TokenUsage usage, double cost)
    {
        TaskId = taskId;
        Usage = usage;
        Cost = cost;
    }
}

/// <summary>
/// Raised when estimated cost exceeds budget.
/// </summary>
public class BudgetExceededException : WaterException
{
    public double CurrentCost { get; }
    public double Budget { get; }

    public BudgetExceededException(double currentCost, double budget)
        : base($"Budget exceeded: ${currentCost:F4} > ${budget:F4}")
    {
        CurrentCost = currentCost;
        Budget = budget;
    }
}

/// <summary>
/// Summary of costs across all tasks.
/// </summary>
public class CostSummary
{
    public double TotalCost { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens => TotalInputTokens + TotalOutputTokens;
    public int TaskCount { get; set; }
    public List<TaskCost> TaskCosts { get; set; } = new();
}

/// <summary>
/// Tracks LLM costs across flow execution.
/// </summary>
public class CostTracker
{
    private readonly List<TaskCost> _costs = new();
    private readonly double? _budget;
    private readonly object _lock = new();

    public double InputCostPer1KTokens { get; set; } = 0.01;
    public double OutputCostPer1KTokens { get; set; } = 0.03;

    public CostTracker(double? budget = null)
    {
        _budget = budget;
    }

    public void RecordUsage(string taskId, int inputTokens, int outputTokens)
    {
        var usage = new TokenUsage { InputTokens = inputTokens, OutputTokens = outputTokens };
        var cost = (inputTokens / 1000.0) * InputCostPer1KTokens + (outputTokens / 1000.0) * OutputCostPer1KTokens;
        var taskCost = new TaskCost(taskId, usage, cost);

        lock (_lock)
        {
            _costs.Add(taskCost);

            if (_budget.HasValue)
            {
                var total = _costs.Sum(c => c.Cost);
                if (total > _budget.Value)
                    throw new BudgetExceededException(total, _budget.Value);
            }
        }
    }

    public CostSummary GetSummary()
    {
        lock (_lock)
        {
            return new CostSummary
            {
                TotalCost = _costs.Sum(c => c.Cost),
                TotalInputTokens = _costs.Sum(c => c.Usage.InputTokens),
                TotalOutputTokens = _costs.Sum(c => c.Usage.OutputTokens),
                TaskCount = _costs.Count,
                TaskCosts = new List<TaskCost>(_costs)
            };
        }
    }
}
