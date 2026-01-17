using DistributedRateLimiter.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistributedRateLimiter.RateLimiting.InMemory;

public class InMemoryTokenBucket : IRateLimiter
{
    private const int Capacity = 10;
    private const double RefillRatePerSecond = 1;
    private readonly Dictionary<string, TokenBucketState> _buckets = new();

    public InMemoryTokenBucket(ILogger<InMemoryTokenBucket> logger)
    {
        // Logger not needed - FallbackRateLimiter handles all logging
    }

    public Task<bool> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;

        lock (_buckets)
        {
            if (!_buckets.TryGetValue(key, out var state))
            {
                state = new TokenBucketState { Tokens = Capacity - 1, LastRefill = now };
                _buckets[key] = state;
                return Task.FromResult(true);
            }

            var elapsed = (now - state.LastRefill).TotalSeconds;
            state.Tokens = Math.Min(Capacity, state.Tokens + elapsed * RefillRatePerSecond);
            state.LastRefill = now;

            if (state.Tokens < 1)
            {
                return Task.FromResult(false);
            }

            state.Tokens -= 1;
            return Task.FromResult(true);
        }
    }

    private class TokenBucketState
    {
        public double Tokens { get; set; }
        public DateTime LastRefill { get; set; }
    }
}
