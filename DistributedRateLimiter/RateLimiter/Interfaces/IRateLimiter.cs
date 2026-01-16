namespace DistributedRateLimiter.RateLimiting.Interfaces;

public interface IRateLimiter
{
    Task<bool> AllowRequestAsync(string key);
}
