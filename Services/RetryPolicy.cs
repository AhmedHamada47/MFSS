namespace MFSS.Services;

public static class RetryPolicy
{
    public static TimeSpan GetDelay(int retryCount)
    {
        var seconds = Math.Min(Math.Pow(2, retryCount + 1), 60);
        var jitter = new Random().NextDouble() * 0.4 + 0.8;
        return TimeSpan.FromSeconds(seconds * jitter);
    }
}
