namespace MFSS.Services;

/// <summary>
/// Thread-safe circuit breaker pattern implementation.
/// Opens the circuit after a threshold of consecutive failures, 
/// preventing further requests until a timeout period elapses.
/// </summary>
public class CircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private readonly Logger _log;
    private int _failureCount;
    private long _lastFailureTicks = DateTime.MinValue.Ticks;
    private int _isOpen; // 0 = closed, 1 = open (using int for Interlocked)

    public CircuitBreaker(int threshold, TimeSpan timeout, Logger log)
    {
        _threshold = threshold;
        _timeout = timeout;
        _log = log;
    }

    public bool AllowRequest()
    {
        if (Interlocked.CompareExchange(ref _isOpen, 0, 0) == 0) return true;

        var lastFailureTime = new DateTime(Interlocked.Read(ref _lastFailureTicks), DateTimeKind.Utc);
        if (DateTime.UtcNow - lastFailureTime > _timeout)
        {
            Interlocked.Exchange(ref _isOpen, 0);
            Interlocked.Exchange(ref _failureCount, 0);
            _log.Info("  🔌 Circuit breaker reset (half-open).");
            return true;
        }
        return false;
    }

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Interlocked.Exchange(ref _isOpen, 0);
    }

    public void RecordFailure()
    {
        var count = Interlocked.Increment(ref _failureCount);
        Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
        if (count >= _threshold)
        {
            Interlocked.Exchange(ref _isOpen, 1);
            _log.Warning($"  🔌 Circuit breaker OPEN after {count} consecutive failures.");
        }
    }
}
