namespace Water.Core;

/// <summary>
/// Raised when a call is attempted while the circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : WaterException
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

/// <summary>
/// Circuit breaker that opens after consecutive failures and recovers after a timeout.
/// States: Closed (normal), Open (rejecting), HalfOpen (testing recovery).
/// </summary>
public class CircuitBreaker
{
    public int FailureThreshold { get; }
    public double RecoveryTimeout { get; }

    private int _failureCount;
    private long? _openedAtTicks;
    private string _state = "closed";
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold = 5, double recoveryTimeout = 30.0)
    {
        FailureThreshold = failureThreshold;
        RecoveryTimeout = recoveryTimeout;
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = "closed";
            _openedAtTicks = null;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            if (_failureCount >= FailureThreshold)
            {
                _state = "open";
                _openedAtTicks = Environment.TickCount64;
            }
        }
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_state == "closed") return true;

            if (_state == "open" && _openedAtTicks.HasValue)
            {
                var elapsed = (Environment.TickCount64 - _openedAtTicks.Value) / 1000.0;
                if (elapsed >= RecoveryTimeout)
                {
                    _state = "half_open";
                    return true;
                }
            }

            return _state == "half_open";
        }
    }

    public string State
    {
        get
        {
            lock (_lock)
            {
                if (_state == "open" && _openedAtTicks.HasValue)
                {
                    var elapsed = (Environment.TickCount64 - _openedAtTicks.Value) / 1000.0;
                    if (elapsed >= RecoveryTimeout)
                        _state = "half_open";
                }
                return _state;
            }
        }
    }
}
