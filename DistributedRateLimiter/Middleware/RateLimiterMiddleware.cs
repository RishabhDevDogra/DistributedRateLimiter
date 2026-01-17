using DistributedRateLimiter.RateLimiting.Interfaces;

namespace DistributedRateLimiter.Middleware;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private static int _allowed = 0;
    private static int _blocked = 0;

    public RateLimiterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IRateLimiter limiter)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "user-123";
        var allowed = await limiter.AllowRequestAsync(key);

        if (allowed)
        {
            Interlocked.Increment(ref _allowed);
        }
        else
        {
            Interlocked.Increment(ref _blocked);
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _next(context);
    }

    public static (int allowed, int blocked) GetMetrics() => (_allowed, _blocked);
}
