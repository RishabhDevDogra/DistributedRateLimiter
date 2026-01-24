namespace DistributedRateLimiter.Configuration;

public class RateLimiterOptions
{
    public const string SectionName = "RateLimiter";

    public int Capacity { get; set; } = 10;
    public int RefillRate { get; set; } = 10;
    public int RefillIntervalSeconds { get; set; } = 60;
    public int RedisHealthCheckIntervalSeconds { get; set; } = 30;
    public bool EnableMetrics { get; set; } = true;
}
