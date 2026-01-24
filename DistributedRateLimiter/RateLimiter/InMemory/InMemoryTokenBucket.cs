using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;

namespace DistributedRateLimiter.RateLimiting.InMemory;

public class InMemoryTokenBucket : IRateLimiter
{
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private readonly Dictionary<string, TokenBucketState> _buckets = new();

    public InMemoryTokenBucket(IOptions<RateLimiterOptions> options)
    {
        var opts = options.Value;
        _capacity = opts.Capacity;
        _refillRatePerSecond = (double)opts.RefillRate / opts.RefillIntervalSeconds;
    }

    public Task<RateLimitResult> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;

        lock (_buckets)
        {
            if (!_buckets.TryGetValue(key, out var state))
            {
                state = new TokenBucketState { Tokens = _capacity - 1, LastRefill = now };
                _buckets[key] = state;
                var resetTime = now.AddSeconds(1.0 / _refillRatePerSecond);
                return Task.FromResult(new RateLimitResult(true, (int)Math.Max(0, state.Tokens), resetTime));
            }

            var elapsed = (now - state.LastRefill).TotalSeconds;
            state.Tokens = Math.Min(_capacity, state.Tokens + elapsed * _refillRatePerSecond);
            state.LastRefill = now;

            if (state.Tokens < 1)
            {
                var resetTime = now.AddSeconds(1.0 / _refillRatePerSecond);
                return Task.FromResult(new RateLimitResult(false, 0, resetTime));
            }

            state.Tokens -= 1;
            var resetTimeAllowed = now.AddSeconds((_capacity - state.Tokens) / _refillRatePerSecond);
            return Task.FromResult(new RateLimitResult(true, (int)Math.Max(0, state.Tokens), resetTimeAllowed));
        }
    }

    private class TokenBucketState
    {
        public double Tokens { get; set; }
        public DateTime LastRefill { get; set; }
    }
}
