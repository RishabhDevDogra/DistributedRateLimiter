using DistributedRateLimiter.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistributedRateLimiter.RateLimiting.InMemory;

public class InMemoryTokenBucket : IRateLimiter
{
    private readonly ILogger<InMemoryTokenBucket> _logger;
    private const int Capacity = 10;
    private const double RefillRatePerSecond = 1;
    private readonly Dictionary<string, TokenBucketState> _buckets = new();

    public InMemoryTokenBucket(ILogger<InMemoryTokenBucket> logger)
    {
        _logger = logger;
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
                LogColored(key, state.Tokens, true, "InMemory");
                return Task.FromResult(true);
            }

            var elapsed = (now - state.LastRefill).TotalSeconds;
            state.Tokens = Math.Min(Capacity, state.Tokens + elapsed * RefillRatePerSecond);
            state.LastRefill = now;

            if (state.Tokens < 1)
            {
                LogColored(key, state.Tokens, false, "InMemory");
                return Task.FromResult(false);
            }

            state.Tokens -= 1;
            LogColored(key, state.Tokens, true, "InMemory");
            return Task.FromResult(true);
        }
    }

    private void LogColored(string key, double tokens, bool allowed, string source)
    {
        if (allowed)
            Console.ForegroundColor = ConsoleColor.Green;
        else
            Console.ForegroundColor = ConsoleColor.Red;

        _logger.LogInformation("{Key} -> {Status} ({Source}) Tokens={Tokens:F2}",
            key,
            allowed ? "Allowed ✅" : "Blocked ❌",
            source,
            tokens);

        Console.ResetColor();
    }

    private class TokenBucketState
    {
        public double Tokens { get; set; }
        public DateTime LastRefill { get; set; }
    }
}
