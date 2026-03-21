using System.Text.Json;

namespace Water.Observability;

/// <summary>
/// Structured log context with key-value pairs.
/// </summary>
public class LogContext
{
    private readonly Dictionary<string, object?> _fields = new();

    public LogContext Set(string key, object? value)
    {
        _fields[key] = value;
        return this;
    }

    public Dictionary<string, object?> Fields => new(_fields);
}

/// <summary>
/// Interface for exporting structured logs.
/// </summary>
public interface ILogExporter
{
    Task ExportAsync(Dictionary<string, object?> logEntry);
}

/// <summary>
/// Console log exporter that writes JSON.
/// </summary>
public class ConsoleLogExporter : ILogExporter
{
    public Task ExportAsync(Dictionary<string, object?> logEntry)
    {
        Console.WriteLine(JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false }));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Structured logger for flow observability.
/// </summary>
public class StructuredLogger
{
    private readonly List<ILogExporter> _exporters = new();
    private readonly LogContext _defaultContext = new();

    public StructuredLogger(ILogExporter? exporter = null)
    {
        if (exporter != null) _exporters.Add(exporter);
        else _exporters.Add(new ConsoleLogExporter());
    }

    public StructuredLogger AddExporter(ILogExporter exporter)
    {
        _exporters.Add(exporter);
        return this;
    }

    public StructuredLogger WithContext(string key, object? value)
    {
        _defaultContext.Set(key, value);
        return this;
    }

    public async Task LogAsync(string level, string message, LogContext? context = null)
    {
        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["level"] = level,
            ["message"] = message
        };

        foreach (var kvp in _defaultContext.Fields)
            entry[kvp.Key] = kvp.Value;

        if (context != null)
            foreach (var kvp in context.Fields)
                entry[kvp.Key] = kvp.Value;

        foreach (var exporter in _exporters)
            await exporter.ExportAsync(entry);
    }

    public Task InfoAsync(string message, LogContext? context = null) => LogAsync("info", message, context);
    public Task WarnAsync(string message, LogContext? context = null) => LogAsync("warn", message, context);
    public Task ErrorAsync(string message, LogContext? context = null) => LogAsync("error", message, context);
    public Task DebugAsync(string message, LogContext? context = null) => LogAsync("debug", message, context);
}
