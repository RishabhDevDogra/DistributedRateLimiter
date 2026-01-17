using DistributedRateLimiter.RateLimiting.Interfaces;
using System.Collections.Concurrent;

namespace DistributedRateLimiter.Middleware;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    
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
        var allowed = await limiter.AllowRequestAsync(key);

        _metrics.AddOrUpdate(
            key,
            allowed ? (1, 0) : (0, 1),
            (k, old) => allowed ? (old.allowed + 1, old.blocked) : (old.allowed, old.blocked + 1)
        );

        if (!allowed)
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
