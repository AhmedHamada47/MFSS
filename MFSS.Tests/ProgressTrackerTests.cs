using MFSS.Services;

namespace MFSS.Tests;

public class ProgressTrackerTests
{
    [Fact]
    public void RecordSuccess_IncrementsCount()
    {
        var tracker = new ProgressTracker(10);
        tracker.RecordSuccess(1024);
        tracker.RecordSuccess(2048);

        var summary = tracker.GetSummary();
        Assert.Matches("Successful:\\s+2", summary);
    }

    [Fact]
    public void RecordFailure_IncrementsCount()
    {
        var tracker = new ProgressTracker(10);
        tracker.RecordFailure();
        tracker.RecordFailure();

        var summary = tracker.GetSummary();
        Assert.Matches("Failed:\\s+2", summary);
        Assert.True(tracker.HasFailures);
    }

    [Fact]
    public void HasFailures_FalseWhenNoFailures()
    {
        var tracker = new ProgressTracker(5);
        tracker.RecordSuccess(100);
        Assert.False(tracker.HasFailures);
    }

    [Fact]
    public void GetSummary_ShowsCorrectRemaining()
    {
        var tracker = new ProgressTracker(10);
        tracker.RecordSuccess(100);
        tracker.RecordSuccess(200);
        tracker.RecordFailure();

        var summary = tracker.GetSummary();
        Assert.Matches("Total Records:\\s+10", summary);
        Assert.Matches("Remaining:\\s+7", summary);
    }

    [Fact]
    public void GetSummary_ShowsDataTransfer()
    {
        var tracker = new ProgressTracker(1);
        tracker.RecordSuccess(1048576); // 1 MB

        var summary = tracker.GetSummary();
        Assert.Contains("1.00 MB", summary);
    }

    [Fact]
    public void IsThreadSafe_ConcurrentUpdates()
    {
        var tracker = new ProgressTracker(1000);

        _ = Parallel.For(0, 1000, i =>
        {
            if (i % 2 == 0) tracker.RecordSuccess(100);
            else tracker.RecordFailure();
        });

        var summary = tracker.GetSummary();
        Assert.Matches("Successful:\\s+50\\d", summary);
        Assert.Matches("Failed:\\s+50\\d", summary);
        Assert.Matches("Total Records: 1000", summary);
    }
}
