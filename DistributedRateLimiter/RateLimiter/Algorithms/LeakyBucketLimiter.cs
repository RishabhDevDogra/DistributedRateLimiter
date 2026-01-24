using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.RateLimiting.Algorithms;

/// <summary>
/// Leaky Bucket algorithm - constant outflow rate, handles bursts smoothly.
/// Simulates a bucket that leaks at constant rate.
/// Best for: Smooth traffic shaping, preventing traffic spikes
/// </summary>
public class LeakyBucketLimiter : IRateLimiter
{
    private readonly int _capacity;
    private readonly double _leakRatePerSecond;
    private readonly ConcurrentDictionary<string, BucketState> _buckets = new();

    public LeakyBucketLimiter(IOptions<RateLimiterOptions> options)
    {
        var opts = options.Value;
        _capacity = opts.Capacity;
        _leakRatePerSecond = (double)opts.RefillRate / opts.RefillIntervalSeconds;
    }

    public Task<RateLimitResult> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;

        var state = _buckets.AddOrUpdate(
            key,
            _ => new BucketState { Water = 0, LastLeakTime = now },
            (_, existing) => existing
        );

        lock (state)
        {
            // Calculate how much water has leaked since last request
            var elapsed = (now - state.LastLeakTime).TotalSeconds;
            state.Water = Math.Max(0, state.Water - elapsed * _leakRatePerSecond);
            state.LastLeakTime = now;

            // Check if request fits
            if (state.Water < _capacity)
            {
                state.Water += 1; // Add request to bucket
                var remaining = (int)Math.Max(0, _capacity - state.Water);
                var resetTime = now.AddSeconds((state.Water) / _leakRatePerSecond);
                return Task.FromResult(new RateLimitResult(true, remaining, resetTime));
            }

            // Bucket is full
            var timeToWait = (state.Water - _capacity + 1) / _leakRatePerSecond;
            var resetTimeBlocked = now.AddSeconds(timeToWait);
            return Task.FromResult(new RateLimitResult(false, 0, resetTimeBlocked));
        }
    }

    private class BucketState
    {
        public double Water { get; set; }
        public DateTime LastLeakTime { get; set; }
    }
}
