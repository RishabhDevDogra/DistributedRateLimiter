using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.RateLimiting.InMemory;
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

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");
// =====================
// RATE-LIMITED ENDPOINT
// =====================
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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
