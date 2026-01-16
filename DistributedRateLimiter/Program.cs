using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.InMemory;
using DistributedRateLimiter.Middleware;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRateLimiter, InMemoryTokenBucket>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RateLimiterMiddleware>();
app.UseHttpsRedirection();


// RATE-LIMITED ENDPOINT for demonstration

app.MapGet("/api/limited", async (IRateLimiter rateLimiter) =>
{
    var key = "user-123"; // temporary user ID
    var allowed = await rateLimiter.AllowRequestAsync(key);

    return allowed
        ? Results.Ok("Request allowed 🚀")
        : Results.Problem("Rate limit exceeded", statusCode: 429);
})
.WithName("RateLimitedEndpoint");
app.Run();

