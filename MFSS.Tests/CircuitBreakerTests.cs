using MFSS.Services;

namespace MFSS.Tests;

public class CircuitBreakerTests
{
    private Logger CreateTestLogger()
    {
        return new Logger("test-circuit-breaker");
    }

    [Fact]
    public void AllowRequest_InitialState_ReturnsTrue()
    {
        using var log = CreateTestLogger();
        var breaker = new CircuitBreaker(3, TimeSpan.FromSeconds(5), log);
        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public void AllowRequest_AfterThresholdFailures_ReturnsFalse()
    {
        using var log = CreateTestLogger();
        var breaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30), log);

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();

        Assert.False(breaker.AllowRequest());
    }

    [Fact]
    public void AllowRequest_AfterTimeout_ReturnsTrue()
    {
        using var log = CreateTestLogger();
        var breaker = new CircuitBreaker(2, TimeSpan.FromMilliseconds(50), log);

        breaker.RecordFailure();
        breaker.RecordFailure();

        Assert.False(breaker.AllowRequest());

        Thread.Sleep(100); // Wait for timeout

        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public void RecordSuccess_ResetsCircuit()
    {
        using var log = CreateTestLogger();
        var breaker = new CircuitBreaker(2, TimeSpan.FromSeconds(30), log);

        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure(); // Only 1 failure now, not 2

        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public async Task IsThreadSafe_ConcurrentAccess()
    {
        using var log = CreateTestLogger();
        var breaker = new CircuitBreaker(100, TimeSpan.FromSeconds(30), log);

        var tasks = Enumerable.Range(0, 200).Select(_ =>
            Task.Run(() =>
            {
                breaker.RecordFailure();
                breaker.AllowRequest();
                breaker.RecordSuccess();
            }));

        await Task.WhenAll(tasks);
    }
}
