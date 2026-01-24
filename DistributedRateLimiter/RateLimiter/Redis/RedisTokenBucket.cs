using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedRateLimiter.RateLimiting.Redis;

public class RedisTokenBucket : IRateLimiter
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisTokenBucket> _logger;
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;

    public RedisTokenBucket(
        IConnectionMultiplexer redis, 
        ILogger<RedisTokenBucket> logger,
        IOptions<RateLimiterOptions> options)
    {
        _db = redis.GetDatabase();
        _logger = logger;
        var opts = options.Value;
        _capacity = opts.Capacity;
        _refillRatePerSecond = (double)opts.RefillRate / opts.RefillIntervalSeconds;
    }

    public async Task<RateLimitResult> AllowRequestAsync(string key)
    {
        try
        {
            var redisKey = $"rate:{key}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
                new RedisValue[] { _capacity, _refillRatePerSecond, now }
            );

            bool allowed = (long)result![0] == 1;
            double tokens = (double)(long)result[1];

            RedisHealth.MarkHealthy();
            
            var remaining = (int)Math.Max(0, tokens);
            var resetTime = DateTime.UtcNow.AddSeconds((_capacity - tokens) / _refillRatePerSecond);
            return new RateLimitResult(allowed, remaining, resetTime);
        }
        catch
        {
            _logger.LogError("Redis error for key {Key}: Connection issue", key);
            throw;
        }
    }
}