using Water.Middleware;
using Water.Storage;

namespace Water.Core;

/// <summary>
/// A workflow orchestrator that allows building and executing complex data processing pipelines.
/// Supports sequential, parallel, branching, loops, map, DAG, try-catch, and agentic loops.
/// </summary>
public class Flow
{
    public string Id { get; set; }
    public string Description { get; set; }
    public string? Version { get; set; }
    public bool StrictContracts { get; set; }
    public int MaxConcurrency { get; set; } = 10;
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public HookManager Hooks { get; set; } = new();
    public EventEmitter? Events { get; set; }
    public List<IMiddleware> MiddlewareList { get; set; } = new();

    public List<ExecutionNode> ExecutionGraph { get; } = new();
    public bool Registered { get; private set; }
    internal IStorageBackend? Storage { get; set; }
    internal object? Checkpoint { get; set; }
    internal IDeadLetterQueue? Dlq { get; set; }
    internal Dictionary<string, object> Services { get; } = new();

    public Flow(
        string? id = null,
        string? description = null,
        IStorageBackend? storage = null,
        string? version = null,
        bool strictContracts = false,
        int maxConcurrency = 10)
    {
        Id = id ?? $"flow_{Guid.NewGuid().ToString("N")[..8]}";
        Description = description ?? $"Flow {Id}";
        Storage = storage;
        Version = version;
        StrictContracts = strictContracts;
        MaxConcurrency = maxConcurrency;
    }

    private void ValidateRegistrationState()
    {
        if (Registered) throw new InvalidOperationException("Cannot add tasks after registration");
    }

    private static void ValidateTask(WaterTask? task)
    {
        if (task is null) throw new ArgumentNullException(nameof(task), "Task cannot be null");
    }

    /// <summary>
    /// Add a middleware to the flow.
    /// </summary>
    public Flow Use(IMiddleware middleware)
    {
        MiddlewareList.Add(middleware);
        return this;
    }

    /// <summary>
    /// Register a shared service for dependency injection into tasks.
    /// </summary>
    public Flow Inject(string name, object service)
    {
        Services[name] = service;
        return this;
    }

    /// <summary>
    /// Set metadata for this flow.
    /// </summary>
    public Flow SetMetadata(string key, object? value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Add a task to execute sequentially.
    /// </summary>
    public Flow Then(
        WaterTask task,
        Func<Dictionary<string, object?>, bool>? when = null,
        WaterTask? fallback = null)
    {
        ValidateRegistrationState();
        ValidateTask(task);

        var node = new ExecutionNode
        {
            Type = NodeType.Sequential,
            Task = task,
            When = when,
            Fallback = fallback
        };
        ExecutionGraph.Add(node);
        return this;
    }

    /// <summary>
    /// Execute a task once per item in a list field, in parallel.
    /// </summary>
    public Flow Map(WaterTask task, string over)
    {
        ValidateRegistrationState();
        ValidateTask(task);
        if (string.IsNullOrEmpty(over))
            throw new ArgumentException("Map 'over' key cannot be empty", nameof(over));

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.Map,
            Task = task,
            Over = over
        });
        return this;
    }

    /// <summary>
    /// Add a DAG (directed acyclic graph) of tasks with automatic parallelization.
    /// </summary>
    public Flow Dag(List<WaterTask> tasks, Dictionary<string, List<string>>? dependencies = null)
    {
        ValidateRegistrationState();
        if (tasks.Count == 0)
            throw new ArgumentException("DAG task list cannot be empty", nameof(tasks));

        foreach (var task in tasks) ValidateTask(task);

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.Dag,
            Tasks = tasks,
            Dependencies = dependencies ?? new()
        });
        return this;
    }

    /// <summary>
    /// Add tasks to execute in parallel.
    /// </summary>
    public Flow Parallel(List<WaterTask> tasks)
    {
        ValidateRegistrationState();
        if (tasks.Count == 0)
            throw new ArgumentException("Parallel task list cannot be empty", nameof(tasks));

        foreach (var task in tasks) ValidateTask(task);

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.Parallel,
            Tasks = tasks
        });
        return this;
    }

    /// <summary>
    /// Add conditional branching logic. Executes the first task whose condition returns True.
    /// </summary>
    public Flow Branch(List<(Func<Dictionary<string, object?>, bool> condition, WaterTask task)> branches)
    {
        ValidateRegistrationState();
        if (branches.Count == 0)
            throw new ArgumentException("Branch list cannot be empty", nameof(branches));

        var branchConditions = branches.Select(b =>
        {
            ValidateTask(b.task);
            return new BranchCondition { Condition = b.condition, Task = b.task };
        }).ToList();

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.Branch,
            Branches = branchConditions
        });
        return this;
    }

    /// <summary>
    /// Execute a task repeatedly while a condition is true.
    /// </summary>
    public Flow Loop(
        Func<Dictionary<string, object?>, bool> condition,
        WaterTask task,
        int maxIterations = 100)
    {
        ValidateRegistrationState();
        ValidateTask(task);

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.Loop,
            Condition = condition,
            Task = task,
            MaxIterations = maxIterations
        });
        return this;
    }

    /// <summary>
    /// Add a try-catch node that executes a fallback task on failure.
    /// </summary>
    public Flow TryCatch(WaterTask task, WaterTask catchTask)
    {
        ValidateRegistrationState();
        ValidateTask(task);
        ValidateTask(catchTask);

        ExecutionGraph.Add(new ExecutionNode
        {
            Type = NodeType.TryCatch,
            Task = task,
            Fallback = catchTask
        });
        return this;
    }

    /// <summary>
    /// Register the flow, making it ready for execution.
    /// </summary>
    public Flow Register()
    {
        if (Registered)
            throw new InvalidOperationException("Flow is already registered");
        Registered = true;
        return this;
    }

    /// <summary>
    /// Execute the flow with the provided input data.
    /// </summary>
    public async Task<Dictionary<string, object?>> RunAsync(Dictionary<string, object?> inputData)
    {
        if (!Registered)
            Register();

        // Emit flow start hook
        await Hooks.EmitAsync("on_flow_start", new Dictionary<string, object?>
        {
            ["flow_id"] = Id,
            ["input_data"] = inputData
        });

        try
        {
            var result = await ExecutionEngine.RunAsync(
                ExecutionGraph,
                inputData,
                Id,
                Metadata,
                Storage,
                hooks: Hooks,
                eventEmitter: Events,
                middleware: MiddlewareList,
                dlq: Dlq,
                services: Services,
                maxConcurrency: MaxConcurrency);

            await Hooks.EmitAsync("on_flow_complete", new Dictionary<string, object?>
            {
                ["flow_id"] = Id,
                ["output_data"] = result
            });

            return result;
        }
        catch (Exception ex) when (ex is not FlowPausedException && ex is not FlowStoppedException)
        {
            await Hooks.EmitAsync("on_flow_error", new Dictionary<string, object?>
            {
                ["flow_id"] = Id,
                ["error"] = ex
            });
            throw;
        }
    }
}
