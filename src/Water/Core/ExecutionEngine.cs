using Microsoft.Extensions.Logging;
using Water.Middleware;
using Water.Resilience;
using Water.Storage;

namespace Water.Core;

/// <summary>
/// Core execution engine for Water flows.
/// Orchestrates sequential tasks, parallel execution, branching, loops, map, DAG, and try-catch.
/// </summary>
public static class ExecutionEngine
{
    private static readonly ILogger _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("ExecutionEngine");
    private static readonly SemaphoreSlim _flowStateLock = new(1, 1);

    public static async Task<Dictionary<string, object?>> RunAsync(
        List<ExecutionNode> executionGraph,
        Dictionary<string, object?> inputData,
        string flowId,
        Dictionary<string, object?>? flowMetadata = null,
        IStorageBackend? storage = null,
        Dictionary<string, object?>? resumeFrom = null,
        HookManager? hooks = null,
        EventEmitter? eventEmitter = null,
        List<IMiddleware>? middleware = null,
        IDeadLetterQueue? dlq = null,
        Dictionary<string, object>? services = null,
        int maxConcurrency = 10)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        ExecutionContext context;
        Dictionary<string, object?> data;
        int startIndex;

        if (resumeFrom != null)
        {
            context = new ExecutionContext(
                flowId,
                resumeFrom.TryGetValue("execution_id", out var eid) ? eid?.ToString() : null,
                flowMetadata: flowMetadata ?? new(),
                inputData: inputData);

            if (resumeFrom.TryGetValue("context_state", out var ctxState) && ctxState is Dictionary<string, object?> state)
            {
                if (state.TryGetValue("step_number", out var sn) && sn is int stepNum)
                    context.StepNumber = stepNum;
            }

            data = resumeFrom.TryGetValue("data", out var d) && d is Dictionary<string, object?> dict
                ? dict : inputData;
            startIndex = resumeFrom.TryGetValue("node_index", out var ni) && ni is int idx ? idx : 0;

            if (services != null)
                foreach (var kvp in services)
                    context.RegisterService(kvp.Key, kvp.Value);
        }
        else
        {
            context = new ExecutionContext(flowId, flowMetadata: flowMetadata ?? new(), inputData: inputData);
            data = inputData;
            startIndex = 0;

            if (services != null)
                foreach (var kvp in services)
                    context.RegisterService(kvp.Key, kvp.Value);
        }

        // Save initial session state
        if (storage != null)
        {
            var session = await storage.GetSessionAsync(context.ExecutionId);
            if (session == null)
            {
                session = new FlowSession(flowId, inputData, context.ExecutionId, FlowStatus.Running);
            }
            else
            {
                session.Status = FlowStatus.Running;
            }
            await storage.SaveSessionAsync(session);
        }

        try
        {
            for (int nodeIndex = startIndex; nodeIndex < executionGraph.Count; nodeIndex++)
            {
                // Check for pause/stop signals
                if (storage != null)
                {
                    await _flowStateLock.WaitAsync();
                    try
                    {
                        var session = await storage.GetSessionAsync(context.ExecutionId);
                        if (session?.Status == FlowStatus.Paused)
                        {
                            session.CurrentNodeIndex = nodeIndex;
                            session.CurrentData = data;
                            session.ContextState = new Dictionary<string, object?>
                            {
                                ["task_outputs"] = context.TaskOutputsInternal,
                                ["step_history"] = context.StepHistoryInternal,
                                ["step_number"] = context.StepNumber
                            };
                            await storage.SaveSessionAsync(session);
                            throw new FlowPausedException($"Flow {flowId} paused at node {nodeIndex} (execution: {context.ExecutionId})");
                        }
                        else if (session?.Status == FlowStatus.Stopped)
                        {
                            session.CurrentNodeIndex = nodeIndex;
                            session.CurrentData = data;
                            await storage.SaveSessionAsync(session);
                            throw new FlowStoppedException($"Flow {flowId} stopped at node {nodeIndex} (execution: {context.ExecutionId})");
                        }
                    }
                    finally
                    {
                        _flowStateLock.Release();
                    }
                }

                var node = executionGraph[nodeIndex];
                data = await ExecuteNodeAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq, semaphore);
            }

            // Mark as completed
            if (storage != null)
            {
                var session = await storage.GetSessionAsync(context.ExecutionId);
                if (session != null)
                {
                    session.Status = FlowStatus.Completed;
                    session.Result = data;
                    session.CurrentData = data;
                    session.CurrentNodeIndex = executionGraph.Count;
                    await storage.SaveSessionAsync(session);
                }
            }
        }
        catch (Exception ex) when (ex is not FlowPausedException && ex is not FlowStoppedException)
        {
            if (storage != null)
            {
                var session = await storage.GetSessionAsync(context.ExecutionId);
                if (session != null)
                {
                    session.Status = FlowStatus.Failed;
                    session.Error = ex.Message;
                    await storage.SaveSessionAsync(session);
                }
            }
            throw;
        }

        return data;
    }

    private static async Task<Dictionary<string, object?>> ExecuteNodeAsync(
        ExecutionNode node,
        Dictionary<string, object?> data,
        ExecutionContext context,
        IStorageBackend? storage,
        int nodeIndex,
        HookManager? hooks,
        EventEmitter? eventEmitter,
        List<IMiddleware>? middleware,
        IDeadLetterQueue? dlq,
        SemaphoreSlim semaphore)
    {
        return node.Type switch
        {
            NodeType.Sequential => await ExecuteSequentialAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq),
            NodeType.Parallel => await ExecuteParallelAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq, semaphore),
            NodeType.Branch => await ExecuteBranchAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq),
            NodeType.Loop => await ExecuteLoopAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq),
            NodeType.Map => await ExecuteMapAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq, semaphore),
            NodeType.Dag => await ExecuteDagAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq, semaphore),
            NodeType.TryCatch => await ExecuteTryCatchAsync(node, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq),
            _ => throw new WaterException($"Unknown node type: {node.Type}")
        };
    }

    internal static async Task<Dictionary<string, object?>> ExecuteTaskAsync(
        WaterTask task,
        Dictionary<string, object?> data,
        ExecutionContext context,
        IStorageBackend? storage = null,
        int nodeIndex = 0,
        HookManager? hooks = null,
        EventEmitter? eventEmitter = null,
        List<IMiddleware>? middleware = null,
        IDeadLetterQueue? dlq = null)
    {
        var inputParams = new Dictionary<string, object?> { ["input_data"] = data };

        // Cache lookup
        if (task.Cache != null)
        {
            var cacheKey = CacheHelper.ComputeKey(task.Id, data);
            var cached = task.Cache.Get(cacheKey);
            if (cached is Dictionary<string, object?> cachedResult)
            {
                context.TaskId = task.Id;
                context.StepNumber++;
                context.AddTaskOutput(task.Id, cachedResult);
                return cachedResult;
            }
        }

        // Circuit breaker check
        if (task.CircuitBreaker != null && !task.CircuitBreaker.CanExecute())
            throw new CircuitBreakerOpenException($"Circuit breaker is open for task '{task.Id}'");

        context.TaskId = task.Id;
        context.StepStartTime = DateTime.UtcNow;
        context.StepNumber++;

        int maxAttempts = task.RetryCount + 1;
        Exception? lastError = null;

        // Emit task start
        if (hooks != null)
        {
            await hooks.EmitAsync("on_task_start", new Dictionary<string, object?>
            {
                ["task_id"] = task.Id,
                ["input_data"] = data,
                ["context"] = context
            });
        }

        if (eventEmitter != null)
        {
            await eventEmitter.EmitAsync(new FlowEvent("task_start", context.FlowId, task.Id, context.ExecutionId, new() { ["input"] = data }));
        }

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            context.AttemptNumber = attempt;

            TaskRun? taskRun = null;
            if (storage != null)
            {
                taskRun = new TaskRun(context.ExecutionId, task.Id, nodeIndex, FlowStatus.Running, data, startedAt: DateTime.UtcNow);
                await storage.SaveTaskRunAsync(taskRun);
            }

            try
            {
                // Rate limiting
                if (task.RateLimit.HasValue)
                    await RateLimiter.Global.AcquireAsync(task.Id, task.RateLimit.Value);

                // Run middleware before_task
                var currentData = data;
                if (middleware != null)
                {
                    foreach (var mw in middleware.OrderBy(m => m.Order))
                        currentData = await mw.BeforeTaskAsync(task.Id, currentData, context);
                    inputParams = new Dictionary<string, object?> { ["input_data"] = currentData };
                }

                // Execute with optional timeout
                Dictionary<string, object?> result;
                if (task.Timeout.HasValue)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(task.Timeout.Value));
                    result = await task.Execute(inputParams, context).WaitAsync(cts.Token);
                }
                else
                {
                    result = await task.Execute(inputParams, context);
                }

                // Run middleware after_task
                if (middleware != null)
                {
                    foreach (var mw in middleware.OrderByDescending(m => m.Order))
                        result = await mw.AfterTaskAsync(task.Id, currentData, result, context);
                }

                // Store in cache
                if (task.Cache != null)
                {
                    var cacheKey = CacheHelper.ComputeKey(task.Id, data);
                    task.Cache.Set(cacheKey, result);
                }

                context.AddTaskOutput(task.Id, result);

                // Update task run record
                if (storage != null && taskRun != null)
                {
                    taskRun.Status = FlowStatus.Completed;
                    taskRun.OutputData = result;
                    taskRun.CompletedAt = DateTime.UtcNow;
                    await storage.SaveTaskRunAsync(taskRun);
                }

                // Emit task complete
                if (hooks != null)
                {
                    await hooks.EmitAsync("on_task_complete", new Dictionary<string, object?>
                    {
                        ["task_id"] = task.Id,
                        ["input_data"] = data,
                        ["output_data"] = result,
                        ["context"] = context
                    });
                }

                if (eventEmitter != null)
                {
                    await eventEmitter.EmitAsync(new FlowEvent("task_complete", context.FlowId, task.Id, context.ExecutionId, new() { ["output"] = result }));
                }

                task.CircuitBreaker?.RecordSuccess();

                return result;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (storage != null && taskRun != null)
                {
                    taskRun.Status = FlowStatus.Failed;
                    taskRun.Error = ex.Message;
                    taskRun.CompletedAt = DateTime.UtcNow;
                    await storage.SaveTaskRunAsync(taskRun);
                }

                if (attempt < maxAttempts)
                {
                    var delay = task.RetryDelay * Math.Pow(task.RetryBackoff, attempt - 1);
                    if (delay > 0)
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                    _logger.LogInformation("Retrying task {TaskId} (attempt {Attempt}/{Max}) after error: {Error}",
                        task.Id, attempt + 1, maxAttempts, ex.Message);
                }
                else
                {
                    task.CircuitBreaker?.RecordFailure();

                    if (hooks != null)
                    {
                        await hooks.EmitAsync("on_task_error", new Dictionary<string, object?>
                        {
                            ["task_id"] = task.Id,
                            ["input_data"] = data,
                            ["error"] = lastError,
                            ["context"] = context
                        });
                    }

                    if (eventEmitter != null)
                    {
                        await eventEmitter.EmitAsync(new FlowEvent("task_error", context.FlowId, task.Id, context.ExecutionId, new() { ["error"] = lastError?.Message }));
                    }

                    // Send to DLQ
                    if (dlq != null)
                    {
                        await dlq.EnqueueAsync(new DeadLetter(task.Id, data, lastError?.Message ?? "Unknown error", context.ExecutionId));
                    }
                }
            }
        }

        throw lastError ?? new WaterException($"Task '{task.Id}' failed after {maxAttempts} attempts");
    }

    private static async Task<Dictionary<string, object?>> ExecuteSequentialAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq)
    {
        // Check conditional execution
        if (node.When != null && !node.When(data))
            return data;

        try
        {
            return await ExecuteTaskAsync(node.Task!, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
        catch (Exception) when (node.Fallback != null)
        {
            return await ExecuteTaskAsync(node.Fallback, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
    }

    private static async Task<Dictionary<string, object?>> ExecuteParallelAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq,
        SemaphoreSlim semaphore)
    {
        var tasks = node.Tasks!.Select(async task =>
        {
            await semaphore.WaitAsync();
            try
            {
                var childContext = context.CreateChildContext(task.Id);
                var result = await ExecuteTaskAsync(task, new Dictionary<string, object?>(data), childContext, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
                return (task.Id, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var merged = new Dictionary<string, object?>(data);
        foreach (var (taskId, result) in results)
        {
            foreach (var kvp in result)
                merged[kvp.Key] = kvp.Value;
            context.AddTaskOutput(taskId, result);
        }
        return merged;
    }

    private static async Task<Dictionary<string, object?>> ExecuteBranchAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq)
    {
        foreach (var branch in node.Branches!)
        {
            if (branch.Condition(data))
                return await ExecuteTaskAsync(branch.Task, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
        return data; // No branch matched, pass through
    }

    private static async Task<Dictionary<string, object?>> ExecuteLoopAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq)
    {
        var currentData = data;
        for (int i = 0; i < node.MaxIterations; i++)
        {
            if (!node.Condition!(currentData))
                break;
            currentData = await ExecuteTaskAsync(node.Task!, currentData, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
        return currentData;
    }

    private static async Task<Dictionary<string, object?>> ExecuteMapAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq,
        SemaphoreSlim semaphore)
    {
        var overKey = node.Over!;
        if (!data.TryGetValue(overKey, out var listObj) || listObj is not IEnumerable<object> items)
            return data;

        var itemList = items.ToList();
        var tasks = itemList.Select(async (item, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var itemData = new Dictionary<string, object?>(data) { [overKey] = item };
                var childContext = context.CreateChildContext($"{node.Task!.Id}_map_{index}");
                return await ExecuteTaskAsync(node.Task!, itemData, childContext, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var merged = new Dictionary<string, object?>(data)
        {
            [overKey] = results.ToList()
        };
        return merged;
    }

    private static async Task<Dictionary<string, object?>> ExecuteDagAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq,
        SemaphoreSlim semaphore)
    {
        var dagTasks = node.Tasks!;
        var deps = node.Dependencies ?? new();
        var completed = new HashSet<string>();
        var results = new Dictionary<string, Dictionary<string, object?>>();
        var remaining = new HashSet<string>(dagTasks.Select(t => t.Id));
        var taskById = dagTasks.ToDictionary(t => t.Id);

        while (remaining.Count > 0)
        {
            // Find tasks with all dependencies satisfied
            var ready = remaining
                .Where(id => !deps.ContainsKey(id) || deps[id].All(d => completed.Contains(d)))
                .ToList();

            if (ready.Count == 0)
                throw new WaterException("DAG has circular dependencies or unresolvable tasks");

            var batchTasks = ready.Select(async taskId =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var childContext = context.CreateChildContext(taskId);
                    var result = await ExecuteTaskAsync(taskById[taskId], new Dictionary<string, object?>(data), childContext, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
                    return (taskId, result);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(batchTasks);
            foreach (var (taskId, result) in batchResults)
            {
                results[taskId] = result;
                completed.Add(taskId);
                remaining.Remove(taskId);
                context.AddTaskOutput(taskId, result);
                foreach (var kvp in result)
                    data[kvp.Key] = kvp.Value;
            }
        }

        return data;
    }

    private static async Task<Dictionary<string, object?>> ExecuteTryCatchAsync(
        ExecutionNode node, Dictionary<string, object?> data, ExecutionContext context,
        IStorageBackend? storage, int nodeIndex, HookManager? hooks,
        EventEmitter? eventEmitter, List<IMiddleware>? middleware, IDeadLetterQueue? dlq)
    {
        try
        {
            return await ExecuteTaskAsync(node.Task!, data, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
        catch (Exception ex)
        {
            var errorData = new Dictionary<string, object?>(data) { ["_error"] = ex.Message };
            return await ExecuteTaskAsync(node.Fallback!, errorData, context, storage, nodeIndex, hooks, eventEmitter, middleware, dlq);
        }
    }
}
