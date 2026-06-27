using MFSS.Services;

namespace MFSS.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void GetDelay_FirstAttempt_ReturnsApproximately1Second()
    {
        var delay = RetryPolicy.GetDelay(0);
        Assert.InRange(delay.TotalMilliseconds, 1000, 1500);
    }

    [Fact]
    public void GetDelay_SecondAttempt_ReturnsApproximately2Seconds()
    {
        var delay = RetryPolicy.GetDelay(1);
        Assert.InRange(delay.TotalMilliseconds, 2000, 2500);
    }

    [Fact]
    public void GetDelay_ThirdAttempt_ReturnsApproximately4Seconds()
    {
        var delay = RetryPolicy.GetDelay(2);
        Assert.InRange(delay.TotalMilliseconds, 4000, 4500);
    }

    [Fact]
    public void GetDelay_IncludesJitter_NotAlwaysSameValue()
    {
        var delays = Enumerable.Range(0, 100).Select(_ => RetryPolicy.GetDelay(0)).ToList();
        // Jitter should cause some variation
        var distinctValues = delays.Select(d => d.TotalMilliseconds).Distinct().Count();
        Assert.True(distinctValues > 1, "Expected jitter to produce varying delay values");
    }
}
