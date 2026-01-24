using DistributedRateLimiter.HealthChecks;
using DistributedRateLimiter.RateLimiting.Fallback;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class RedisHealthCheckTests
{
    private readonly Mock<IConnectionMultiplexer> _redisConnectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisHealthCheck>> _loggerMock;
    private readonly RedisHealthCheck _healthCheck;

    public RedisHealthCheckTests()
    {
        _redisConnectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisHealthCheck>>();

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _healthCheck = new RedisHealthCheck(_redisConnectionMock.Object, _loggerMock.Object);
        
        RedisHealth.Reset();
    }

    [Fact]
    public async Task WhenRedisRespondsQuickly_ShouldReturnHealthy()
    {
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(5));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("healthy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenRedisIsSlow_ShouldReturnDegraded()
    {
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1500)); // > 1 second

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("slowly", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenRedisIsDown_ShouldReturnUnhealthy()
    {
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Connection failed"));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not available", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenRedisResponds_ShouldMarkHealthy()
    {
        RedisHealth.MarkFailure();
        
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(5));

        await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.True(RedisHealth.IsAvailable);
    }

    [Fact]
    public async Task WhenRedisFails_ShouldMarkFailed()
    {
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Connection failed"));

        await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.False(RedisHealth.IsAvailable);
    }

    [Fact]
    public async Task HealthResponse_ShouldIncludeFallbackStatus()
    {
        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(5));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.True(result.Data.ContainsKey("fallback_status"));
        Assert.Equal("InMemory limiter available if needed", result.Data["fallback_status"]);
    }

    [Fact]
    public async Task HealthResponse_ShouldIncludeMetrics()
    {
        RedisHealth.MarkFailure();
        RedisHealth.MarkFailure();

        _databaseMock
            .Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(5));

        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.True(result.Data.ContainsKey("redis_failure_count"));
        Assert.Equal(2, (int)result.Data["redis_failure_count"]);
    }
}
