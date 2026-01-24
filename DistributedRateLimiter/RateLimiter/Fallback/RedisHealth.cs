namespace DistributedRateLimiter.RateLimiting.Fallback;

public static class RedisHealth
{
    private static volatile bool _isAvailable = true;
    private static DateTime _lastFailure = DateTime.MinValue;
    private static int _failureCount = 0;
    private static int _successCount = 0;

    // Redis considered available if healthy OR 5s have passed since last failure (retry sooner)
    public static bool IsAvailable =>
        _isAvailable || DateTime.UtcNow - _lastFailure > TimeSpan.FromSeconds(5);

    public static DateTime LastFailure => _lastFailure;
    public static int FailureCount => _failureCount;
    public static int SuccessCount => _successCount;

    public static void MarkFailure()
    {
        _isAvailable = false;
        _lastFailure = DateTime.UtcNow;
        Interlocked.Increment(ref _failureCount);
    }

    public static void MarkHealthy()
    {
        _isAvailable = true;
        Interlocked.Increment(ref _successCount);
    }

    public static void Reset()
    {
        _isAvailable = true;
        _lastFailure = DateTime.MinValue;
        _failureCount = 0;
        _successCount = 0;
    }
}
