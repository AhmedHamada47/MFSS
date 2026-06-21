using System.Diagnostics;

namespace MFSS.Services;

public class ProgressTracker
{
    private readonly int _total;
    private int _ok, _fail;
    private long _bytes;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly object _lock = new();

    public ProgressTracker(int total) { _total = total; }
    public int Success => _ok;
    public int Failed => _fail;
    public long TotalBytes => _bytes;

    public void RecordSuccess(long size) { lock (_lock) { _ok++; _bytes += size; } }
    public void RecordFailure() { lock (_lock) { _fail++; } }

    public string GetProgress()
    {
        lock (_lock)
        {
            var done = _ok + _fail;
            var pct = _total > 0 ? (double)done / _total * 100 : 0;
            var mb = _bytes / (1024.0 * 1024.0);
            return $"  ✅{_ok} ❌{_fail} | {pct:F0}% | {mb:F1}MB";
        }
    }

    public string GetSummary() => GetProgress();
}
