namespace DistributedRateLimiter.RateLimiting.Interfaces;

public record RateLimitResult(bool Allowed, int Remaining, DateTime ResetTime);

public interface IRateLimiter
{
    Task<RateLimitResult> AllowRequestAsync(string key);
}
