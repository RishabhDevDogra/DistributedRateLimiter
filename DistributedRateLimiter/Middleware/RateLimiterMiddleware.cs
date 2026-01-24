using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.Middleware;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimiterMiddleware> _logger;
    private readonly RateLimiterOptions _options;
    
    // Track allowed/blocked per user
    private static ConcurrentDictionary<string, (int allowed, int blocked)> _metrics 
        = new ConcurrentDictionary<string, (int allowed, int blocked)>();

    public RateLimiterMiddleware(
        RequestDelegate next, 
        ILogger<RateLimiterMiddleware> logger,
        IOptions<RateLimiterOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Invoke(HttpContext context, IRateLimiter limiter)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogDebug("Processing request for client {ClientKey}", key);
        
        var result = await limiter.AllowRequestAsync(key);

        // Add rate limit headers to response
        context.Response.Headers["X-RateLimit-Limit"] = _options.Capacity.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ((long)result.ResetTime.Subtract(DateTime.UnixEpoch).TotalSeconds).ToString();

        if (_options.EnableMetrics)
        {
            _metrics.AddOrUpdate(
                key,
                result.Allowed ? (1, 0) : (0, 1),
                (k, old) => result.Allowed ? (old.allowed + 1, old.blocked) : (old.allowed, old.blocked + 1)
            );
        }

        if (!result.Allowed)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientKey}. Remaining: {Remaining}, Reset: {ResetTime}", 
                key, result.Remaining, result.ResetTime);
            
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        _logger.LogDebug("Request allowed for client {ClientKey}. Remaining: {Remaining}", 
            key, result.Remaining);

        await _next(context);
    }

    // Get metrics for all users
    public static IDictionary<string, (int allowed, int blocked)> GetMetrics() => _metrics;
}
