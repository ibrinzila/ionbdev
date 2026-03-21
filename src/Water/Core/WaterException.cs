namespace Water.Core;

/// <summary>
/// Base exception for all Water framework errors.
/// </summary>
public class WaterException : Exception
{
    public WaterException() { }
    public WaterException(string message) : base(message) { }
    public WaterException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Raised when a flow execution is paused.
/// </summary>
public class FlowPausedException : WaterException
{
    public FlowPausedException(string message) : base(message) { }
}

/// <summary>
/// Raised when a flow execution is stopped.
/// </summary>
public class FlowStoppedException : WaterException
{
    public FlowStoppedException(string message) : base(message) { }
}
