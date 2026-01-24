using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Redis;
using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.RateLimiting.InMemory;
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
    ConnectionMultiplexer.Connect(connectionString));

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

// Rate-limited endpoint
app.MapGet("/api/limited", () =>
{
    return Results.Ok(new 
    { 
        message = "Request allowed 🚀",
        timestamp = DateTime.UtcNow
    });
})
.WithName("RateLimitedEndpoint")
.WithTags("RateLimiter")
.WithOpenApi(op => new(op) { Summary = "Rate limited endpoint - Protected by token bucket algorithm (10 requests per minute)" })
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
