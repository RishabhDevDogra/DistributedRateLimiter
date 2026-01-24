using DistributedRateLimiter.RateLimiting.Redis;
using DistributedRateLimiter.RateLimiting.Fallback;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class RedisTokenBucketTests
{
    private readonly Mock<IConnectionMultiplexer> _redisConnectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisTokenBucket>> _loggerMock;
    private readonly IOptions<RateLimiterOptions> _options;
    private readonly RedisTokenBucket _redisLimiter;

    public RedisTokenBucketTests()
    {
        _redisConnectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisTokenBucket>>();
        _options = Options.Create(new RateLimiterOptions
        {
            Capacity = 10,
            RefillRate = 10,
            RefillIntervalSeconds = 60
        });

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _redisLimiter = new RedisTokenBucket(_redisConnectionMock.Object, _loggerMock.Object, _options);
    }

    [Fact]
    public async Task FirstRequest_ShouldBeAllowed()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var luaResult = new RedisResult[] { (long)1, (long)9 };

        _databaseMock
            .Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>()))
            .ReturnsAsync((RedisResult)luaResult);

        var result = await _redisLimiter.AllowRequestAsync("test-user");

        Assert.True(result.Allowed);
        Assert.Equal(9, result.Remaining);
    }

    [Fact]
    public async Task WhenRedisThrows_ShouldThrow()
    {
        _databaseMock
            .Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Connection failed"));

        await Assert.ThrowsAsync<RedisConnectionException>(
            () => _redisLimiter.AllowRequestAsync("test-user"));
    }

    [Fact]
    public async Task SuccessfulRequest_ShouldMarkHealthy()
    {
        RedisHealth.Reset();
        
        var luaResult = new RedisResult[] { (long)1, (long)9 };

        _databaseMock
            .Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>()))
            .ReturnsAsync((RedisResult)luaResult);

        await _redisLimiter.AllowRequestAsync("test-user");

        Assert.True(RedisHealth.IsAvailable);
    }
}
