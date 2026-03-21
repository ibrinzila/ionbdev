namespace Water.Resilience;

/// <summary>
/// Token-bucket rate limiter for throttling task execution.
/// </summary>
public class RateLimiter
{
    private static readonly Lazy<RateLimiter> _global = new(() => new RateLimiter());
    public static RateLimiter Global => _global.Value;

    private readonly Dictionary<string, Bucket> _buckets = new();
    private readonly object _lock = new();

    public async Task AcquireAsync(string name, double maxPerSecond)
    {
        Bucket bucket;
        lock (_lock)
        {
            if (!_buckets.TryGetValue(name, out bucket!))
            {
                bucket = new Bucket(maxPerSecond);
                _buckets[name] = bucket;
            }
        }
        await bucket.AcquireAsync();
    }

    private class Bucket
    {
        private readonly double _maxPerSecond;
        private readonly double _interval;
        private double _tokens;
        private readonly double _maxTokens;
        private long _lastRefillTicks;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public Bucket(double maxPerSecond)
        {
            _maxPerSecond = maxPerSecond;
            _interval = 1.0 / maxPerSecond;
            _tokens = 1.0;
            _maxTokens = 1.0;
            _lastRefillTicks = Environment.TickCount64;
        }

        public async Task AcquireAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var now = Environment.TickCount64;
                var elapsed = (now - _lastRefillTicks) / 1000.0;
                _tokens = Math.Min(_maxTokens, _tokens + elapsed * _maxPerSecond);
                _lastRefillTicks = now;

                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return;
                }

                var waitTime = (1.0 - _tokens) / _maxPerSecond;
                _tokens = 0.0;
                await Task.Delay(TimeSpan.FromSeconds(waitTime));
                _lastRefillTicks = Environment.TickCount64;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
