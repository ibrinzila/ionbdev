using Water.Core;

namespace Water.Server;

/// <summary>
/// Registry for managing flows within the server.
/// Registered as a singleton in DI.
/// </summary>
public class FlowRegistry
{
    private readonly Dictionary<string, Flow> _flows = new();

    public FlowRegistry() { }

    public FlowRegistry(IEnumerable<Flow> flows)
    {
        foreach (var flow in flows)
            Register(flow);
    }

    public FlowRegistry Register(Flow flow)
    {
        if (_flows.ContainsKey(flow.Id))
            throw new ArgumentException($"Duplicate flow ID: {flow.Id}");
        if (!flow.Registered)
            flow.Register();
        _flows[flow.Id] = flow;
        return this;
    }

    public Flow? Get(string flowId) => _flows.TryGetValue(flowId, out var flow) ? flow : null;

    public IReadOnlyList<Flow> GetAll() => _flows.Values.ToList();

    public int Count => _flows.Count;
}
