using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class FallbackRateLimiterTests
{
    private readonly Mock<IRateLimiter> _redisLimiterMock;
    private readonly Mock<IRateLimiter> _inMemoryLimiterMock;
    private readonly Mock<ILogger<FallbackRateLimiter>> _loggerMock;
    private readonly FallbackRateLimiter _fallbackLimiter;

    public FallbackRateLimiterTests()
    {
        _redisLimiterMock = new Mock<IRateLimiter>();
        _inMemoryLimiterMock = new Mock<IRateLimiter>();
        _loggerMock = new Mock<ILogger<FallbackRateLimiter>>();
        _fallbackLimiter = new FallbackRateLimiter(
            _redisLimiterMock.Object,
            _inMemoryLimiterMock.Object,
            _loggerMock.Object
        );
        
        // Reset Redis health for each test
        RedisHealth.Reset();
    }

    [Fact]
    public async Task WhenRedisAvailable_ShouldUseRedis()
    {
        var key = "test-user";
        var expectedResult = new RateLimitResult(true, 9, DateTime.UtcNow.AddSeconds(10));
        
        _redisLimiterMock
            .Setup(x => x.AllowRequestAsync(key))
            .ReturnsAsync(expectedResult);

        var result = await _fallbackLimiter.AllowRequestAsync(key);

        Assert.True(result.Allowed);
        Assert.Equal(9, result.Remaining);
        _redisLimiterMock.Verify(x => x.AllowRequestAsync(key), Times.Once);
        _inMemoryLimiterMock.Verify(x => x.AllowRequestAsync(key), Times.Never);
    }

    [Fact]
    public async Task WhenRedisFails_ShouldFallbackToInMemory()
    {
        var key = "test-user";
        var inMemoryResult = new RateLimitResult(true, 9, DateTime.UtcNow.AddSeconds(10));

        _redisLimiterMock
            .Setup(x => x.AllowRequestAsync(key))
            .ThrowsAsync(new Exception("Redis connection failed"));

        _inMemoryLimiterMock
            .Setup(x => x.AllowRequestAsync(key))
            .ReturnsAsync(inMemoryResult);

        var result = await _fallbackLimiter.AllowRequestAsync(key);

        Assert.True(result.Allowed);
        Assert.Equal(9, result.Remaining);
        _redisLimiterMock.Verify(x => x.AllowRequestAsync(key), Times.Once);
        _inMemoryLimiterMock.Verify(x => x.AllowRequestAsync(key), Times.Once);
    }
}
