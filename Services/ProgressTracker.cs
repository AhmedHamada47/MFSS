namespace MFSS.Services;

public class ProgressTracker
{
    private readonly int _total;
    private int _success;
    private int _failed;
    private long _totalBytes;

    public ProgressTracker(int total)
    {
        _total = total;
    }

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
        var mbTransferred = _totalBytes / (1024.0 * 1024.0);
        return $@"
  Total Records: {_total}
  Successful:    {_success}
  Failed:        {_failed}
  Remaining:     {_total - _success - _failed}
  Data Transfer: {mbTransferred:F2} MB";
    }
}
