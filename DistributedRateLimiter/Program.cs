using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Redis;
using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.RateLimiting.InMemory;
using DistributedRateLimiter.Middleware;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger for API docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis connection (non-abort, retry automatically)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false")
);

// Concrete limiters
builder.Services.AddSingleton<RedisTokenBucket>();
builder.Services.AddSingleton<InMemoryTokenBucket>();

// Fallback limiter (Redis → InMemory)
builder.Services.AddSingleton<IRateLimiter>(sp =>
{
    var redis = sp.GetRequiredService<RedisTokenBucket>();
    var memory = sp.GetRequiredService<InMemoryTokenBucket>();
    var logger = sp.GetRequiredService<ILogger<FallbackRateLimiter>>();
    return new FallbackRateLimiter(redis, memory, logger);
});

var app = builder.Build();

// Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Rate limiter middleware
app.UseMiddleware<RateLimiterMiddleware>();

app.UseHttpsRedirection();

// Rate-limited endpoint
app.MapGet("/api/limited", async (IRateLimiter limiter, HttpContext ctx) =>
{
    // Use remote IP as key
    var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var allowed = await limiter.AllowRequestAsync(key);

    if (!allowed) return Results.StatusCode(429); // Too Many Requests
    return Results.Ok("Request allowed 🚀");
})
.WithName("RateLimitedEndpoint");

// Metrics endpoint
app.MapGet("/api/metrics", () =>
{
    var metrics = RateLimiterMiddleware.GetMetrics();

    var result = metrics.ToDictionary(
        kvp => kvp.Key,
        kvp => new
        {
            allowed = kvp.Value.allowed,
            blocked = kvp.Value.blocked
        }
    );

    return Results.Ok(result);
})
.WithName("Metrics");

app.Run();
