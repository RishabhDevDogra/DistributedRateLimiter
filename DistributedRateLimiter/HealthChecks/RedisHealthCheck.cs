using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using DistributedRateLimiter.RateLimiting.Fallback;

namespace DistributedRateLimiter.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var pingTime = await db.PingAsync();

            // Mark as healthy in fallback tracker
            RedisHealth.MarkHealthy();

            if (pingTime.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Redis ping time is high: {PingTime}ms", pingTime.TotalMilliseconds);
                return HealthCheckResult.Degraded(
                    $"Redis is responding slowly ({pingTime.TotalMilliseconds}ms)",
                    data: new Dictionary<string, object>
                    {
                        ["ping_ms"] = pingTime.TotalMilliseconds,
                        ["connected"] = _redis.IsConnected,
                        ["redis_circuit_open"] = RedisHealth.IsAvailable,
                        ["redis_failure_count"] = RedisHealth.FailureCount,
                        ["redis_success_count"] = RedisHealth.SuccessCount,
                        ["fallback_status"] = "InMemory limiter is active and healthy"
                    });
            }

            _logger.LogDebug("Redis health check passed. Ping: {PingTime}ms", pingTime.TotalMilliseconds);
            
            return HealthCheckResult.Healthy(
                $"Redis is healthy ({pingTime.TotalMilliseconds}ms)",
                data: new Dictionary<string, object>
                {
                    ["ping_ms"] = pingTime.TotalMilliseconds,
                    ["connected"] = _redis.IsConnected,
                    ["redis_circuit_open"] = RedisHealth.IsAvailable,
                    ["redis_failure_count"] = RedisHealth.FailureCount,
                    ["redis_success_count"] = RedisHealth.SuccessCount,
                    ["last_failure"] = RedisHealth.LastFailure,
                    ["fallback_status"] = "InMemory limiter available if needed"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            
            // Mark as failed in fallback tracker
            RedisHealth.MarkFailure();
            
            return HealthCheckResult.Unhealthy(
                "Redis is not available - using in-memory fallback",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["connected"] = _redis.IsConnected,
                    ["error"] = ex.Message,
                    ["redis_circuit_open"] = RedisHealth.IsAvailable,
                    ["redis_failure_count"] = RedisHealth.FailureCount,
                    ["last_failure"] = RedisHealth.LastFailure,
                    ["fallback_status"] = "InMemory limiter is active and healthy"
                });
        }
    }
}
