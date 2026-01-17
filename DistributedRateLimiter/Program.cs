using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.Redis;
using DistributedRateLimiter.Middleware;
using StackExchange.Redis;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379")
);

// Adding Redis limiter implementation
builder.Services.AddSingleton<IRateLimiter, RedisTokenBucket>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RateLimiterMiddleware>();
app.UseHttpsRedirection();

//Rate limited endpoint
app.MapGet("/api/limited", () =>
{
    // Middleware already handled limiting
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

