namespace MFSS.Services;

public enum CircuitState { Closed, Open, HalfOpen }

public class CircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _cooldown;
    private readonly Logger _log;
    private readonly object _lock = new();
    private CircuitState _state = CircuitState.Closed;
    private int _failures = 0;
    private DateTime _lastFail = DateTime.MinValue;
    public int TotalTripped { get; private set; } = 0;

    public CircuitBreaker(int threshold, TimeSpan cooldown, Logger log)
    { _threshold = threshold; _cooldown = cooldown; _log = log; }

    public CircuitState State
    {
        get { lock (_lock) {
            if (_state == CircuitState.Open && DateTime.UtcNow - _lastFail >= _cooldown)
            { _state = CircuitState.HalfOpen; _log.Warning("  🔌 Circuit HALF-OPEN"); }
            return _state;
        }}
    }

    public bool AllowRequest() => State != CircuitState.Open;

    public void RecordSuccess() { lock (_lock) { _failures = 0; _state = CircuitState.Closed; } }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failures++; _lastFail = DateTime.UtcNow;
            if (_failures >= _threshold && _state != CircuitState.Open)
            { _state = CircuitState.Open; TotalTripped++; _log.Error($"  🔌 Circuit OPEN: {_failures} failures!"); }
        }
    }

    public async Task<bool> WaitForAllowedAsync(CancellationToken ct)
    {
        while (!AllowRequest()) { if (ct.IsCancellationRequested) return false; await Task.Delay(1000, ct); }
        return true;
    }

    public string GetStatus() => $"State={State}, Failures={_failures}/{_threshold}";
}
