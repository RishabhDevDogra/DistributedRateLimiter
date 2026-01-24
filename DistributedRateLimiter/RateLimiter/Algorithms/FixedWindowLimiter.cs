using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.RateLimiting.Algorithms;

/// <summary>
/// Fixed Window algorithm - simple counter that resets at fixed intervals.
/// Fastest, simplest, but allows burst at window edges.
/// Best for: Simple rate limiting, high throughput
/// </summary>
public class FixedWindowLimiter : IRateLimiter
{
    private readonly int _capacity;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, WindowState> _windows = new();

    public FixedWindowLimiter(IOptions<RateLimiterOptions> options)
    {
        var opts = options.Value;
        _capacity = opts.Capacity;
        _windowSeconds = opts.RefillIntervalSeconds;
    }

    public Task<RateLimitResult> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;

        var state = _windows.AddOrUpdate(
            key,
            _ => new WindowState { Count = 0, WindowStart = now },
            (_, existing) =>
            {
                if (now >= existing.WindowStart.AddSeconds(_windowSeconds))
                    return new WindowState { Count = 0, WindowStart = now };
                return existing;
            }
        );

        lock (state)
        {
            // Check if window reset since AddOrUpdate
            if (now >= state.WindowStart.AddSeconds(_windowSeconds))
            {
                state.Count = 0;
                state.WindowStart = now;
            }

            if (state.Count < _capacity)
            {
                state.Count++;
                var remaining = _capacity - state.Count;
                var resetTime = state.WindowStart.AddSeconds(_windowSeconds);
                return Task.FromResult(new RateLimitResult(true, remaining, resetTime));
            }

            // Rate limited
            var resetTimeBlocked = state.WindowStart.AddSeconds(_windowSeconds);
            return Task.FromResult(new RateLimitResult(false, 0, resetTimeBlocked));
        }
    }

    private class WindowState
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}
