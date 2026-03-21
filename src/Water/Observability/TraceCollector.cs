namespace Water.Observability;

/// <summary>
/// A single span within a trace.
/// </summary>
public class TraceSpan
{
    public string SpanId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; }
    public string? ParentSpanId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = new();
    public string Status { get; set; } = "ok";

    public TraceSpan(string name, string? parentSpanId = null)
    {
        Name = name;
        ParentSpanId = parentSpanId;
    }

    public void End(string status = "ok")
    {
        EndTime = DateTime.UtcNow;
        Status = status;
    }

    public double? DurationMs => EndTime.HasValue ? (EndTime.Value - StartTime).TotalMilliseconds : null;
}

/// <summary>
/// Represents a complete trace of a flow execution.
/// </summary>
public class Trace
{
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public string FlowId { get; set; }
    public List<TraceSpan> Spans { get; set; } = new();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    public Trace(string flowId)
    {
        FlowId = flowId;
    }

    public void AddSpan(TraceSpan span) => Spans.Add(span);

    public void End()
    {
        EndTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Store for completed traces.
/// </summary>
public class TraceStore
{
    private readonly List<Trace> _traces = new();
    private readonly object _lock = new();

    public void Add(Trace trace)
    {
        lock (_lock) { _traces.Add(trace); }
    }

    public List<Trace> GetAll()
    {
        lock (_lock) { return new List<Trace>(_traces); }
    }

    public Trace? GetByTraceId(string traceId)
    {
        lock (_lock) { return _traces.FirstOrDefault(t => t.TraceId == traceId); }
    }
}

/// <summary>
/// Collects traces for flow executions.
/// </summary>
public class TraceCollector
{
    private readonly TraceStore _store;
    private Trace? _currentTrace;

    public TraceCollector(TraceStore? store = null)
    {
        _store = store ?? new TraceStore();
    }

    public Trace StartTrace(string flowId)
    {
        _currentTrace = new Trace(flowId);
        return _currentTrace;
    }

    public TraceSpan StartSpan(string name, string? parentSpanId = null)
    {
        var span = new TraceSpan(name, parentSpanId);
        _currentTrace?.AddSpan(span);
        return span;
    }

    public void EndTrace()
    {
        if (_currentTrace != null)
        {
            _currentTrace.End();
            _store.Add(_currentTrace);
            _currentTrace = null;
        }
    }

    public TraceStore Store => _store;
}
