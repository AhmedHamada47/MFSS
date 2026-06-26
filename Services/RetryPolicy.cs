namespace MFSS.Services;

public static class RetryPolicy
{
    /// <summary>
    /// Exponential backoff with jitter.
    /// </summary>
    public static TimeSpan GetDelay(int attempt)
    {
        var baseDelay = Math.Pow(2, attempt) * 1000; // 1s, 2s, 4s, 8s...
        var jitter = Random.Shared.Next(0, 500);
        return TimeSpan.FromMilliseconds(baseDelay + jitter);
    }
}
