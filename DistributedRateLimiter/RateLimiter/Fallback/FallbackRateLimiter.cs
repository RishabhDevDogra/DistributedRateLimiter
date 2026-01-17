using DistributedRateLimiter.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistributedRateLimiter.RateLimiting.Fallback;

public class FallbackRateLimiter : IRateLimiter
{
    private readonly IRateLimiter _redisLimiter;
    private readonly IRateLimiter _inMemoryLimiter;
    private readonly ILogger<FallbackRateLimiter> _logger;

    public FallbackRateLimiter(
        IRateLimiter redisLimiter,
        IRateLimiter inMemoryLimiter,
        ILogger<FallbackRateLimiter> logger)
    {
        _redisLimiter = redisLimiter;
        _inMemoryLimiter = inMemoryLimiter;
        _logger = logger;
    }

    public async Task<RateLimitResult> AllowRequestAsync(string key)
    {
        // 🔥 Try Redis first if available
        if (RedisHealth.IsAvailable)
        {
            try
            {
                var result = await _redisLimiter.AllowRequestAsync(key);

                if (result.Allowed)
                    _logger.LogInformation("{Key} -> Allowed (Redis) | Remaining: {Remaining}", key, result.Remaining);
                else
                    _logger.LogInformation("{Key} -> Blocked (Redis)", key);

                return result;
            }
            catch
            {
                // Redis failed → mark failure and fallback
                RedisHealth.MarkFailure();
                _logger.LogWarning("{Key} -> Redis down → using in-memory limiter", key);
            }
        }

        // 🔥 Redis down → fallback instantly
        var memResult = await _inMemoryLimiter.AllowRequestAsync(key);

        if (memResult.Allowed)
            _logger.LogInformation("{Key} -> Allowed (InMemory) | Remaining: {Remaining}", key, memResult.Remaining);
        else
            _logger.LogInformation("{Key} -> Blocked (InMemory)", key);

        return memResult;
    }
}
