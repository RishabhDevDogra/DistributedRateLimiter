namespace DistributedRateLimiter.RateLimiting.Fallback;

public static class RedisHealth
{
    private static volatile bool _isAvailable = true;
    private static DateTime _lastFailure = DateTime.MinValue;

    // Redis considered available if healthy OR 10s have passed since last failure
    public static bool IsAvailable =>
        _isAvailable || DateTime.UtcNow - _lastFailure > TimeSpan.FromSeconds(10);

    public static void MarkFailure()
    {
        _isAvailable = false;
        _lastFailure = DateTime.UtcNow;
    }

    public static void MarkHealthy()
    {
        _isAvailable = true;
    }
}
