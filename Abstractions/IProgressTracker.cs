namespace MFSS.Abstractions;

public interface IProgressTracker
{
    bool HasFailures { get; }
    int Success { get; }
    int Failed { get; }
    int Total { get; }
    long TotalBytes { get; }
    void RecordSuccess(long bytes);
    void RecordFailure();
    string GetSummary();
}
