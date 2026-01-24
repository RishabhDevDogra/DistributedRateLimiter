using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.RateLimiting.Algorithms;

/// <summary>
/// Sliding Window algorithm - tracks exact request times within a time window.
/// Most accurate but more memory intensive.
/// Best for: Strict fairness requirements
/// </summary>
public class SlidingWindowLimiter : IRateLimiter
{
    private readonly int _capacity;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _windows = new();
    private readonly Random _random = new();

    public SlidingWindowLimiter(IOptions<RateLimiterOptions> options)
    {
        var opts = options.Value;
        _capacity = opts.Capacity;
        _windowSeconds = opts.RefillIntervalSeconds;
    }

    public Task<RateLimitResult> AllowRequestAsync(string key)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-_windowSeconds);

        // Periodically clean up stale entries (every 100 requests, ~1% overhead)
        if (_random.Next(100) == 0)
        {
            var staleKeys = _windows
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var staleKey in staleKeys)
                _windows.TryRemove(staleKey, out _);
        }

        var queue = _windows.AddOrUpdate(
            key,
            _ => new Queue<DateTime>(),
            (_, existing) => existing
        );

        lock (queue)
        {
            // Remove requests outside the window
            while (queue.Count > 0 && queue.Peek() < windowStart)
            {
                queue.Dequeue();
            }

            // Check if we can allow this request
            if (queue.Count < _capacity)
            {
                queue.Enqueue(now);
                var remaining = _capacity - queue.Count;
                var resetTime = queue.Count > 0 
                    ? queue.Peek().AddSeconds(_windowSeconds) 
                    : now.AddSeconds(_windowSeconds);
                
                return Task.FromResult(new RateLimitResult(true, remaining, resetTime));
            }

            // Rate limited
            var oldestRequest = queue.Peek();
            var resetTimeBlocked = oldestRequest.AddSeconds(_windowSeconds);
            return Task.FromResult(new RateLimitResult(false, 0, resetTimeBlocked));
        }
    }
}
