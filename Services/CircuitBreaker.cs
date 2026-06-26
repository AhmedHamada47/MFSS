namespace MFSS.Services;

public class CircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private readonly Logger _log;
    private int _failureCount;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private bool _isOpen;

    public CircuitBreaker(int threshold, TimeSpan timeout, Logger log)
    {
        _threshold = threshold;
        _timeout = timeout;
        _log = log;
    }

    public bool AllowRequest()
    {
        if (!_isOpen) return true;
        if (DateTime.UtcNow - _lastFailureTime > _timeout)
        {
            _isOpen = false;
            _failureCount = 0;
            _log.Info("  🔌 Circuit breaker reset (half-open).");
            return true;
        }
        return false;
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _isOpen = false;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
        if (_failureCount >= _threshold)
        {
            _isOpen = true;
            _log.Warning($"  🔌 Circuit breaker OPEN after {_failureCount} consecutive failures.");
        }
    }
}
