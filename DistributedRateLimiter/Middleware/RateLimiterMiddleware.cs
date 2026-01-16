using DistributedRateLimiter.RateLimiting.Interfaces;

namespace DistributedRateLimiter.Middleware;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;

    public RateLimiterMiddleware(RequestDelegate next, IRateLimiter rateLimiter)
    {
        _next = next;
        _rateLimiter = rateLimiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Example key: IP address
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        System.Diagnostics.Debugger.Break(); // Breakpoint here
        var allowed = await _rateLimiter.AllowRequestAsync(key);

        if (!allowed)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _next(context);
    }
}
