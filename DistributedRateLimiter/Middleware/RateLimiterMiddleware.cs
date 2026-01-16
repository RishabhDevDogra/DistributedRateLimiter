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
        // Step 1: get a dynamic key
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        // Step 2: ask rate limiter
        var allowed = await _rateLimiter.AllowRequestAsync(key);

        if (!allowed)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        // Step 3: continue to next middleware/endpoint
        await _next(context);
    }

}
