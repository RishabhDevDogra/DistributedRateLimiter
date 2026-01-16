using System.Collections.Concurrent;
using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Models;

namespace DistributedRateLimiter.RateLimiting.InMemory;

public class InMemoryTokenBucket : IRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucketState> _buckets = new();

    private const int Capacity = 10;
    private const double RefillRatePerSecond = 1;

    public Task<bool> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;

        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucketState
        {
            Tokens = Capacity,
            LastRefillUtc = now
        });

        lock (bucket)
        {
            var elapsedSeconds = (now - bucket.LastRefillUtc).TotalSeconds;
            var refill = elapsedSeconds * RefillRatePerSecond;

            bucket.Tokens = Math.Min(Capacity, bucket.Tokens + refill);
            bucket.LastRefillUtc = now;

            if (bucket.Tokens >= 1)
            {
                bucket.Tokens -= 1;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
