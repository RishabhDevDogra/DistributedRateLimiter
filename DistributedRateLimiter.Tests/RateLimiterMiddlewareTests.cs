using DistributedRateLimiter.Middleware;
using DistributedRateLimiter.RateLimiting.Interfaces;
using DistributedRateLimiter.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class RateLimiterMiddlewareTests
{
    private readonly Mock<ILogger<RateLimiterMiddleware>> _loggerMock;
    private readonly IOptions<RateLimiterOptions> _options;

    public RateLimiterMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RateLimiterMiddleware>>();
        _options = Options.Create(new RateLimiterOptions
        {
            Capacity = 10,
            RefillRate = 10,
            RefillIntervalSeconds = 60,
            EnableMetrics = true
        });
    }
    [Fact]
    public async Task ShouldAddRateLimitHeaders_WhenAllowed()
    {
        var rateLimiterMock = new Mock<IRateLimiter>();
        var resetTime = DateTime.UtcNow.AddSeconds(10);
        var allowedResult = new RateLimitResult(true, 5, resetTime);

        rateLimiterMock
            .Setup(x => x.AllowRequestAsync(It.IsAny<string>()))
            .ReturnsAsync(allowedResult);

        var middleware = new RateLimiterMiddleware(ctx => Task.CompletedTask, _loggerMock.Object, _options);
        var httpContext = new DefaultHttpContext();

        await middleware.Invoke(httpContext, rateLimiterMock.Object);

        Assert.Equal("10", httpContext.Response.Headers["X-RateLimit-Limit"]);
        Assert.Equal("5", httpContext.Response.Headers["X-RateLimit-Remaining"]);
        Assert.True(httpContext.Response.Headers.ContainsKey("X-RateLimit-Reset"));
    }

    [Fact]
    public async Task ShouldReturn429_WhenBlocked()
    {
        var rateLimiterMock = new Mock<IRateLimiter>();
        var blockedResult = new RateLimitResult(false, 0, DateTime.UtcNow.AddSeconds(10));

        rateLimiterMock
            .Setup(x => x.AllowRequestAsync(It.IsAny<string>()))
            .ReturnsAsync(blockedResult);

        var middleware = new RateLimiterMiddleware(ctx => Task.CompletedTask, _loggerMock.Object, _options);
        var httpContext = new DefaultHttpContext();

        await middleware.Invoke(httpContext, rateLimiterMock.Object);

        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ShouldCallNextMiddleware_WhenAllowed()
    {
        var nextCalled = false;
        var rateLimiterMock = new Mock<IRateLimiter>();
        var allowedResult = new RateLimitResult(true, 5, DateTime.UtcNow.AddSeconds(10));

        rateLimiterMock
            .Setup(x => x.AllowRequestAsync(It.IsAny<string>()))
            .ReturnsAsync(allowedResult);

        var middleware = new RateLimiterMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object, _options);

        await middleware.Invoke(new DefaultHttpContext(), rateLimiterMock.Object);

        Assert.True(nextCalled);
    }
}
