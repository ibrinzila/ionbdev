namespace Water.Agents;

/// <summary>
/// Represents a tool that an agent can use.
/// </summary>
public class Tool
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object?> InputSchema { get; set; }
    public Func<Dictionary<string, object?>, Task<ToolResult>> ExecuteAsync { get; set; }

    public Tool(
        string name,
        string description,
        Dictionary<string, object?> inputSchema,
        Func<Dictionary<string, object?>, Task<ToolResult>> execute)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        ExecuteAsync = execute;
    }
}

/// <summary>
/// Result from a tool execution.
/// </summary>
public class ToolResult
{
    public bool Success { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }

    public static ToolResult Ok(object? output) => new() { Success = true, Output = output };
    public static ToolResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// A collection of tools.
/// </summary>
public class Toolkit
{
    private readonly Dictionary<string, Tool> _tools = new();

    public Toolkit(IEnumerable<Tool>? tools = null)
    {
        if (tools != null)
            foreach (var tool in tools)
                _tools[tool.Name] = tool;
    }

    public Toolkit Add(Tool tool)
    {
        _tools[tool.Name] = tool;
        return this;
    }

    public Tool? Get(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    public IReadOnlyList<Tool> All => _tools.Values.ToList();

    public int Count => _tools.Count;
}

/// <summary>
/// Executes tools by name from a toolkit.
/// </summary>
public class ToolExecutor
{
    private readonly Toolkit _toolkit;

    public ToolExecutor(Toolkit toolkit)
    {
        _toolkit = toolkit;
    }

    public async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object?> args)
    {
        var tool = _toolkit.Get(toolName);
        if (tool == null)
            return ToolResult.Fail($"Tool '{toolName}' not found");

        try
        {
            return await tool.ExecuteAsync(args);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Tool '{toolName}' failed: {ex.Message}");
        }
    }
}
