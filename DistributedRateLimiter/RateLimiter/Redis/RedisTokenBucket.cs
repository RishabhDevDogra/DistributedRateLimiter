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

    // Returns allowed + tokens
    public async Task<(bool allowed, double tokens)> AllowRequestWithTokensAsync(string key)
    {
        var redisKey = $"rate:{key}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // seconds for Lua

        var lua = @"
            local key = KEYS[1]
            local capacity = tonumber(ARGV[1])
            local refill = tonumber(ARGV[2])
            local now = tonumber(ARGV[3])

            local data = redis.call('GET', key)
            local tokens = capacity

            if data then
                local decoded = cjson.decode(data)
                if decoded and decoded.last and decoded.tokens then
                    local elapsed = now - decoded.last
                    tokens = math.min(capacity, decoded.tokens + elapsed * refill)
                end
            end

            local allowed = 0
            if tokens >= 1 then
                tokens = tokens - 1
                allowed = 1
            end

            local new_data = cjson.encode({tokens = tokens, last = now})
            redis.call('SET', key, new_data)

            return {allowed, tokens}
        ";

        var result = (RedisResult[]?)await _db.ScriptEvaluateAsync(
            lua,
            new RedisKey[] { redisKey },
            new RedisValue[] { Capacity, RefillRatePerSecond, now }
        );

        bool allowed = (long)result![0] == 1;
        double tokens = (double)(long)result[1];

        RedisHealth.MarkHealthy();
        return (allowed, tokens);
    }

    public async Task<bool> AllowRequestAsync(string key)
    {
        try
        {
            var (allowed, _) = await AllowRequestWithTokensAsync(key);
            return allowed;
        }
        catch
        {
            _logger.LogError("Redis error for key {Key}: Connection issue", key);
            throw;
        }
    }
}