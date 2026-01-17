using DistributedRateLimiter.RateLimiting.Interfaces;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.Middleware;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private const int Capacity = 10;
    
    // Track allowed/blocked per user
    private static ConcurrentDictionary<string, (int allowed, int blocked)> _metrics 
        = new ConcurrentDictionary<string, (int allowed, int blocked)>();

    public RateLimiterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IRateLimiter limiter)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "user-123";
        var result = await limiter.AllowRequestAsync(key);

        // Add rate limit headers to response
        context.Response.Headers["X-RateLimit-Limit"] = Capacity.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ((long)result.ResetTime.Subtract(DateTime.UnixEpoch).TotalSeconds).ToString();

        _metrics.AddOrUpdate(
            key,
            result.Allowed ? (1, 0) : (0, 1),
            (k, old) => result.Allowed ? (old.allowed + 1, old.blocked) : (old.allowed, old.blocked + 1)
        );

        if (!result.Allowed)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _next(context);
    }

    // Get metrics for all users
    public static IDictionary<string, (int allowed, int blocked)> GetMetrics() => _metrics;
}
