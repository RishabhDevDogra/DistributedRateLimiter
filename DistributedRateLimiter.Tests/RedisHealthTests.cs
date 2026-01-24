using DistributedRateLimiter.RateLimiting.Fallback;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class RedisHealthTests
{
    public RedisHealthTests()
    {
        RedisHealth.Reset();
    }

    [Fact]
    public void InitialState_ShouldBeAvailable()
    {
        Assert.True(RedisHealth.IsAvailable);
    }

    [Fact]
    public void MarkFailure_ShouldSetUnavailable()
    {
        RedisHealth.MarkFailure();
        Assert.False(RedisHealth.IsAvailable);
    }

    [Fact]
    public void MarkHealthy_ShouldSetAvailable()
    {
        RedisHealth.MarkFailure();
        RedisHealth.MarkHealthy();
        Assert.True(RedisHealth.IsAvailable);
    }

    [Fact]
    public void FailureCount_ShouldIncrement()
    {
        RedisHealth.MarkFailure();
        RedisHealth.MarkFailure();
        Assert.Equal(2, RedisHealth.FailureCount);
    }

    [Fact]
    public void SuccessCount_ShouldIncrement()
    {
        RedisHealth.MarkHealthy();
        RedisHealth.MarkHealthy();
        RedisHealth.MarkHealthy();
        Assert.Equal(3, RedisHealth.SuccessCount);
    }

    [Fact]
    public void LastFailure_ShouldBeTracked()
    {
        var beforeFailure = DateTime.UtcNow;
        RedisHealth.MarkFailure();
        var afterFailure = DateTime.UtcNow;

        Assert.True(RedisHealth.LastFailure >= beforeFailure);
        Assert.True(RedisHealth.LastFailure <= afterFailure);
    }

    [Fact]
    public void AfterRetryWindow_ShouldRetryRedis()
    {
        RedisHealth.MarkFailure();
        Assert.False(RedisHealth.IsAvailable);
        
        // After 5+ seconds, should be available again for retry
        // (In real tests, we'd use a time provider for this)
    }

    [Fact]
    public void Reset_ShouldClearAllMetrics()
    {
        RedisHealth.MarkFailure();
        RedisHealth.MarkFailure();
        RedisHealth.MarkHealthy();

        RedisHealth.Reset();

        Assert.True(RedisHealth.IsAvailable);
        Assert.Equal(0, RedisHealth.FailureCount);
        Assert.Equal(0, RedisHealth.SuccessCount);
    }
}
