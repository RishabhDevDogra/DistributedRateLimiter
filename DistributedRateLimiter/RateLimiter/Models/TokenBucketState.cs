namespace DistributedRateLimiter.RateLimiting.Models;

public class TokenBucketState
{
    public double Tokens { get; set; }
    public DateTime LastRefillUtc { get; set; }
}
