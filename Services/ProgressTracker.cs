using MFSS.Abstractions;

namespace MFSS.Services;

public class ProgressTracker : IProgressTracker
{
    private readonly int _total;
    private int _success;
    private int _failed;
    private long _totalBytes;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public ProgressTracker(int total)
    {
        _total = total;
    }

    public bool HasFailures => Interlocked.CompareExchange(ref _failed, 0, 0) > 0;

    public int Success => Interlocked.CompareExchange(ref _success, 0, 0);

    public int Failed => Interlocked.CompareExchange(ref _failed, 0, 0);

    public int Total => _total;

    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public void RecordSuccess(long bytes)
    {
        Interlocked.Increment(ref _success);
        Interlocked.Add(ref _totalBytes, bytes);
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _failed);
    }

    public string GetSummary()
    {
        var elapsed = DateTime.UtcNow - _startTime;
        var mbTransferred = _totalBytes / (1024.0 * 1024.0);
        var throughput = elapsed.TotalSeconds > 0 ? mbTransferred / elapsed.TotalSeconds : 0;
        return $@"
  Total Records: {_total}
  Successful:    {_success}
  Failed:        {_failed}
  Remaining:     {_total - _success - _failed}
  Data Transfer: {mbTransferred:F2} MB
  Duration:      {elapsed:hh\:mm\:ss}
  Throughput:    {throughput:F2} MB/s";
    }
}
