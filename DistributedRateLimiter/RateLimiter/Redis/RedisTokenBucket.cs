using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Fallback;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace DistributedRateLimiter.RateLimiting.Redis;

public class RedisTokenBucket : IRateLimiter
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisTokenBucket> _logger;

    private const int Capacity = 10;
    private const double RefillRatePerSecond = 1;

    public RedisTokenBucket(IConnectionMultiplexer redis, ILogger<RedisTokenBucket> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> AllowRequestAsync(string key)
    {
        if (!RedisHealth.IsAvailable)
            throw new Exception("Redis unavailable");

        var redisKey = $"rate:{key}";
        var now = DateTime.UtcNow;

        try
        {
            var data = await _db.StringGetAsync(redisKey);
            TokenBucketState state;

            if (string.IsNullOrWhiteSpace(data))
            {
                state = new TokenBucketState { Tokens = Capacity - 1, LastRefill = now };
            }
            else
            {
                state = JsonSerializer.Deserialize<TokenBucketState>(data!) ??
                        new TokenBucketState { Tokens = Capacity, LastRefill = now };

                var elapsed = (now - state.LastRefill).TotalSeconds;
                state.Tokens = Math.Min(Capacity, state.Tokens + elapsed * RefillRatePerSecond);
                state.LastRefill = now;

                if (state.Tokens < 1)
                {
                    _ = _db.StringSetAsync(redisKey, JsonSerializer.Serialize(state));
                    LogColored(key, state.Tokens, false, "Redis");
                    return false;
                }

                state.Tokens -= 1;
            }

            _ = _db.StringSetAsync(redisKey, JsonSerializer.Serialize(state));
            RedisHealth.MarkHealthy();
            LogColored(key, state.Tokens, true, "Redis");
            return true;
        }
        catch
        {
            RedisHealth.MarkFailure();
            _logger.LogWarning("{Key} -> Redis failed → fallback triggered", key);
            throw;
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
