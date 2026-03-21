using Water.Core;

namespace Water.Agents;

/// <summary>
/// Defines the role of an agent in a multi-agent system.
/// </summary>
public class AgentRole
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string SystemPrompt { get; set; }
    public ILlmProvider Provider { get; set; }

    public AgentRole(string name, string description, string systemPrompt, ILlmProvider provider)
    {
        Name = name;
        Description = description;
        SystemPrompt = systemPrompt;
        Provider = provider;
    }
}

/// <summary>
/// Shared context for multi-agent communication.
/// </summary>
public class SharedContext
{
    private readonly Dictionary<string, object?> _data = new();
    private readonly List<Dictionary<string, object?>> _messages = new();
    private readonly object _lock = new();

    public void Set(string key, object? value)
    {
        lock (_lock) { _data[key] = value; }
    }

    public object? Get(string key)
    {
        lock (_lock) { return _data.TryGetValue(key, out var v) ? v : null; }
    }

    public void AddMessage(string from, string to, string content)
    {
        lock (_lock)
        {
            _messages.Add(new()
            {
                ["from"] = from, ["to"] = to, ["content"] = content,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            });
        }
    }

    public List<Dictionary<string, object?>> GetMessages(string? forAgent = null)
    {
        lock (_lock)
        {
            var msgs = forAgent != null
                ? _messages.Where(m => m["to"]?.ToString() == forAgent || m["from"]?.ToString() == forAgent).ToList()
                : new List<Dictionary<string, object?>>(_messages);
            return msgs;
        }
    }
}

/// <summary>
/// Orchestrates multiple agents working together.
/// </summary>
public class AgentOrchestrator
{
    public List<AgentRole> Agents { get; } = new();
    public SharedContext Context { get; } = new();

    public AgentOrchestrator(IEnumerable<AgentRole>? agents = null)
    {
        if (agents != null) Agents.AddRange(agents);
    }

    public AgentOrchestrator AddAgent(AgentRole agent)
    {
        Agents.Add(agent);
        return this;
    }

    public async Task<Dictionary<string, object?>> RunRoundAsync(string prompt)
    {
        var results = new Dictionary<string, object?>();

        foreach (var agent in Agents)
        {
            var messages = new List<Dictionary<string, string>>
            {
                new() { ["role"] = "system", ["content"] = agent.SystemPrompt },
                new() { ["role"] = "user", ["content"] = prompt }
            };

            var result = await agent.Provider.CompleteAsync(messages);
            var response = result.TryGetValue("text", out var text) ? text?.ToString() ?? "" : "";

            results[agent.Name] = response;
            Context.Set($"{agent.Name}_last_response", response);
        }

        return results;
    }
}

/// <summary>
/// Factory for creating a multi-agent team as a single task.
/// </summary>
public static class MultiAgentFactory
{
    public static WaterTask CreateTeam(
        List<AgentRole> agents,
        string? id = null,
        string? description = null)
    {
        var orchestrator = new AgentOrchestrator(agents);

        return new WaterTask(
            execute: async (parameters, context) =>
            {
                var inputData = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;

                var prompt = inputData.TryGetValue("prompt", out var p) ? p?.ToString() ?? "" : "";
                return await orchestrator.RunRoundAsync(prompt);
            },
            id: id ?? $"team_{Guid.NewGuid().ToString("N")[..8]}",
            description: description ?? "Multi-agent team");
    }
}
