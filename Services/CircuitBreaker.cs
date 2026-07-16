using MFSS.Abstractions;

namespace MFSS.Services;

public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private readonly ILogger _log;
    private int _failureCount;
    private long _lastFailureTicks = DateTime.MinValue.Ticks;
    private int _isOpen;

    private const int Closed = 0;
    private const int Open = 1;

    public CircuitBreaker(int threshold, TimeSpan timeout, ILogger log)
    {
        _threshold = threshold;
        _timeout = timeout;
        _log = log;
    }

    public bool AllowRequest()
    {
        if (Volatile.Read(ref _isOpen) == Closed) return true;

        if (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastFailureTicks), DateTimeKind.Utc) <= _timeout)
            return false;

        if (Interlocked.CompareExchange(ref _isOpen, Closed, Open) != Open)
            return false;

        Interlocked.Exchange(ref _failureCount, 0);
        _log.Info("Circuit breaker reset (half-open).");
        return true;
    }

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Interlocked.Exchange(ref _isOpen, Closed);
    }

    public void RecordFailure()
    {
        var count = Interlocked.Increment(ref _failureCount);
        Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
        if (count >= _threshold)
        {
            Interlocked.Exchange(ref _isOpen, Open);
            _log.Warning($"Circuit breaker OPEN after {count} consecutive failures.");
        }
    }
}
