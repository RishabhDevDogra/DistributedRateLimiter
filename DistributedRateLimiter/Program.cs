using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Redis;
using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.RateLimiting.InMemory;
using DistributedRateLimiter.RateLimiting.Algorithms;
using DistributedRateLimiter.Middleware;
using DistributedRateLimiter.Configuration;
using DistributedRateLimiter.HealthChecks;
using StackExchange.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure options
builder.Services.Configure<RateLimiterOptions>(
    builder.Configuration.GetSection(RateLimiterOptions.SectionName));

// Add Swagger for API docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis connection (from configuration)
var redisConfig = builder.Configuration.GetSection("Redis");
var connectionString = $"{redisConfig["ConnectionString"]}," +
                      $"abortConnect={redisConfig["AbortOnConnectFail"]}," +
                      $"connectTimeout={redisConfig["ConnectTimeout"]}," +
                      $"syncTimeout={redisConfig["SyncTimeout"]}";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(connectionString);
    }
    catch (Exception ex)
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to connect to Redis on startup. App will use in-memory limiter only");
        // Return null - app will still function with in-memory limiter via fallback pattern
        return null!;
    }
});

// Concrete limiters
builder.Services.AddSingleton<RedisTokenBucket>();
builder.Services.AddSingleton<InMemoryTokenBucket>();
builder.Services.AddSingleton<SlidingWindowLimiter>();
builder.Services.AddSingleton<FixedWindowLimiter>();
builder.Services.AddSingleton<LeakyBucketLimiter>();

// Fallback limiter (Redis → InMemory)
builder.Services.AddSingleton<IRateLimiter>(sp =>
{
    var redis = sp.GetRequiredService<RedisTokenBucket>();
    var memory = sp.GetRequiredService<InMemoryTokenBucket>();
    var logger = sp.GetRequiredService<ILogger<FallbackRateLimiter>>();
    return new FallbackRateLimiter(redis, memory, logger);
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready", "redis" });

var app = builder.Build();

// Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health check endpoints
app.MapGet("/health/live", () => Results.Ok(new 
{ 
    status = "Healthy",
    description = "Application is running",
    timestamp = DateTime.UtcNow
}))
    .WithName("Liveness")
    .WithTags("Health")
    .WithOpenApi(op => new(op) { Summary = "App liveness check - Is the application process running?" })
    .Produces(200);

app.MapGet("/health/ready", async (HealthCheckService healthCheckService) =>
{
    var result = await healthCheckService.CheckHealthAsync(
        check => check.Tags.Contains("ready"));
    
    return result.Status == HealthStatus.Healthy 
        ? Results.Ok(new 
        { 
            status = result.Status.ToString(),
            description = "Application and dependencies (Redis) are ready",
            timestamp = DateTime.UtcNow
        })
        : Results.Json(new 
        { 
            status = result.Status.ToString(),
            description = "Application or dependencies are not ready",
            timestamp = DateTime.UtcNow
        }, statusCode: 503);
})
.WithName("Readiness")
.WithTags("Health")
.WithOpenApi(op => new(op) { Summary = "App readiness check - Is the application ready to serve traffic? (checks Redis)" })
.Produces(200)
.Produces(503);

app.MapGet("/health", async (HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    
    var response = new
    {
        status = report.Status.ToString(),
        timestamp = DateTime.UtcNow,
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            data = e.Value.Data
        })
    };
    
    return report.Status == HealthStatus.Healthy 
        ? Results.Ok(response)
        : Results.Json(response, statusCode: 503);
})
.WithName("Health")
.WithTags("Health")
.WithOpenApi(op => new(op) { Summary = "Detailed health check - Full diagnostics for all components (Redis, fallback metrics)" })
.Produces(200)
.Produces(503);

// Rate limiter middleware
app.UseMiddleware<RateLimiterMiddleware>();

app.UseHttpsRedirection();

// Token Bucket endpoint (default rate limiter with Redis + fallback)
app.MapGet("/api/limited/token-bucket", (IRateLimiter rateLimiter, HttpContext context) =>
{
    return Results.Ok(new 
    { 
        message = "Request allowed 🚀",
        algorithm = "Token Bucket",
        description = "Smooth traffic with burst capacity. Tokens refill at constant rate (10/min). Best for: general purpose rate limiting.",
        timestamp = DateTime.UtcNow
    });
})
.WithName("TokenBucketEndpoint")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Token Bucket algorithm - Distributed (Redis with in-memory fallback) for burst-friendly limiting" })
.Produces(200)
.Produces(429);

// Sliding Window endpoint (tracks exact request times)
app.MapGet("/api/limited/sliding-window", (SlidingWindowLimiter limiter, HttpContext context) =>
{
    return Results.Ok(new 
    { 
        message = "Request allowed 🚀",
        algorithm = "Sliding Window",
        description = "Most accurate rate limiting. Tracks exact request times in a rolling window (10 requests per minute). Best for: strict compliance, precise quota enforcement.",
        timestamp = DateTime.UtcNow
    });
})
.WithName("SlidingWindowEndpoint")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Sliding Window algorithm - Most accurate, tracks exact request timestamps in rolling window" })
.Produces(200)
.Produces(429);

// Leaky Bucket endpoint (constant outflow rate)
app.MapGet("/api/limited/leaky-bucket", (LeakyBucketLimiter limiter, HttpContext context) =>
{
    return Results.Ok(new 
    { 
        message = "Request allowed 🚀",
        algorithm = "Leaky Bucket",
        description = "Smooth traffic shaping with constant leak rate. Prevents bursts, maintains steady throughput (10 requests per minute). Best for: traffic shaping, protecting backend servers.",
        timestamp = DateTime.UtcNow
    });
})
.WithName("LeakyBucketEndpoint")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Leaky Bucket algorithm - Traffic shaping with constant outflow rate for smooth throughput" })
.Produces(200)
.Produces(429);

// Fixed Window endpoint (simple counter reset)
app.MapGet("/api/limited/fixed-window", (FixedWindowLimiter limiter, HttpContext context) =>
{
    return Results.Ok(new 
    { 
        message = "Request allowed 🚀",
        algorithm = "Fixed Window",
        description = "Simplest and fastest algorithm. Counter resets at fixed intervals (10 requests per minute). Best for: lightweight use cases, high-throughput scenarios.",
        timestamp = DateTime.UtcNow
    });
})
.WithName("FixedWindowEndpoint")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Fixed Window algorithm - Simple counter-based approach with periodic resets" })
.Produces(200)
.Produces(429);

// Metrics endpoint
app.MapGet("/api/metrics", () =>
{
    var metrics = RateLimiterMiddleware.GetMetrics();

    var result = new
    {
        timestamp = DateTime.UtcNow,
        totalClients = metrics.Count,
        clients = metrics.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                allowed = kvp.Value.allowed,
                blocked = kvp.Value.blocked,
                total = kvp.Value.allowed + kvp.Value.blocked
            }
        )
    };

    return Results.Ok(result);
})
.WithName("Metrics")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Rate limiter metrics - View request statistics per client (allowed/blocked counts)" })
.Produces(200);

app.Run();
