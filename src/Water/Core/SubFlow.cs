namespace Water.Core;

/// <summary>
/// Wraps a registered Flow as a Task for composition within another Flow.
/// </summary>
public class SubFlow
{
    public Flow InnerFlow { get; }
    public Dictionary<string, string> InputMapping { get; }
    public Dictionary<string, string> OutputMapping { get; }
    public string? Id { get; }
    public string? Description { get; }

    public SubFlow(
        Flow flow,
        Dictionary<string, string>? inputMapping = null,
        Dictionary<string, string>? outputMapping = null,
        string? id = null,
        string? description = null)
    {
        InnerFlow = flow;
        InputMapping = inputMapping ?? new();
        OutputMapping = outputMapping ?? new();
        Id = id;
        Description = description;
    }

    public WaterTask AsTask()
    {
        var taskId = Id ?? $"subflow_{InnerFlow.Id}_{Guid.NewGuid().ToString("N")[..6]}";
        var inputMapping = InputMapping;
        var outputMapping = OutputMapping;
        var flow = InnerFlow;

        return new WaterTask(
            execute: async (parameters, context) =>
            {
                var data = parameters.TryGetValue("input_data", out var inputObj) && inputObj is Dictionary<string, object?> inputDict
                    ? inputDict
                    : parameters;

                // Apply input mapping
                var mappedInput = new Dictionary<string, object?>(data);
                foreach (var (target, source) in inputMapping)
                {
                    if (data.TryGetValue(source, out var value))
                        mappedInput[target] = value;
                }

                var result = await flow.RunAsync(mappedInput);

                // Apply output mapping
                if (outputMapping.Count > 0)
                {
                    var mappedOutput = new Dictionary<string, object?>();
                    foreach (var (target, source) in outputMapping)
                    {
                        if (result.TryGetValue(source, out var value))
                            mappedOutput[target] = value;
                    }
                    return mappedOutput;
                }

                return result;
            },
            id: taskId,
            description: Description ?? $"SubFlow: {flow.Id}");
    }
}

/// <summary>
/// Compose multiple flows sequentially into a new flow.
/// </summary>
public static class FlowComposer
{
    public static Flow Compose(params Flow[] flows) => Compose(null, null, flows);

    public static Flow Compose(string? id, string? description, params Flow[] flows)
    {
        var flowId = id ?? $"composed_{string.Join("_", flows.Select(f => f.Id))}";
        var composed = new Flow(id: flowId, description: description ?? $"Composed flow: {flowId}");

        foreach (var flow in flows)
        {
            var sub = new SubFlow(flow);
            composed.Then(sub.AsTask());
        }

        return composed;
    }
}
