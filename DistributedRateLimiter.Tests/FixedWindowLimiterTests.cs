using Xunit;
using Microsoft.Extensions.Options;
using DistributedRateLimiter.RateLimiting.Algorithms;
using DistributedRateLimiter.Configuration;
using DistributedRateLimiter.RateLimiting.Interfaces;

namespace DistributedRateLimiter.Tests;

public class FixedWindowLimiterTests
{
    private readonly IOptions<RateLimiterOptions> _options;
    private readonly FixedWindowLimiter _limiter;

    public FixedWindowLimiterTests()
    {
        _options = Options.Create(new RateLimiterOptions
        {
            Capacity = 10,
            RefillRate = 10,
            RefillIntervalSeconds = 60
        });
        _limiter = new FixedWindowLimiter(_options);
    }

    [Fact]
    public async Task AllowRequestAsync_AllowsRequestWithinWindow()
    {
        // Arrange
        var key = "test-user";

        // Act
        var result = await _limiter.AllowRequestAsync(key);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(9, result.Remaining);
        Assert.True(result.ResetTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task AllowRequestAsync_BlocksRequestAfterCapacityExhausted()
    {
        // Arrange
        var key = "test-user";
        
        // Act - Consume all 10 tokens
        for (int i = 0; i < 10; i++)
        {
            var result = await _limiter.AllowRequestAsync(key);
            Assert.True(result.Allowed);
        }
        
        // Request that should be blocked
        var blockedResult = await _limiter.AllowRequestAsync(key);

        // Assert
        Assert.False(blockedResult.Allowed);
        Assert.Equal(0, blockedResult.Remaining);
    }

    [Fact]
    public async Task AllowRequestAsync_IsolatesToknesPerKey()
    {
        // Arrange
        var key1 = "user-1";
        var key2 = "user-2";

        // Act - Exhaust key1
        for (int i = 0; i < 10; i++)
        {
            await _limiter.AllowRequestAsync(key1);
        }

        var key1Result = await _limiter.AllowRequestAsync(key1);
        var key2Result = await _limiter.AllowRequestAsync(key2);

        // Assert - key1 is blocked but key2 is still allowed
        Assert.False(key1Result.Allowed);
        Assert.True(key2Result.Allowed);
        Assert.Equal(9, key2Result.Remaining);
    }

    [Fact]
    public async Task AllowRequestAsync_ReturnsCorrectResetTime()
    {
        // Arrange
        var key = "test-user";
        var beforeRequest = DateTime.UtcNow;

        // Act
        var result = await _limiter.AllowRequestAsync(key);
        var afterRequest = DateTime.UtcNow;

        // Assert
        Assert.True(result.ResetTime > beforeRequest);
        Assert.True(result.ResetTime <= afterRequest.AddSeconds(60));
    }

    [Fact]
    public async Task AllowRequestAsync_IsThreadSafe()
    {
        // Arrange
        var key = "concurrent-user";
        var tasks = new List<Task<RateLimitResult>>();

        // Act - Launch 20 concurrent requests (capacity is 10)
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(_limiter.AllowRequestAsync(key));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var allowed = results.Count(r => r.Allowed);
        var blocked = results.Count(r => !r.Allowed);
        
        Assert.Equal(10, allowed);
        Assert.Equal(10, blocked);
    }

    [Fact]
    public async Task AllowRequestAsync_TrackingMultipleKeysConcurrently()
    {
        // Arrange
        var keys = Enumerable.Range(1, 5).Select(i => $"user-{i}").ToList();
        var tasks = new List<Task<RateLimitResult>>();

        // Act - Each key makes 15 requests
        foreach (var key in keys)
        {
            for (int i = 0; i < 15; i++)
            {
                tasks.Add(_limiter.AllowRequestAsync(key));
            }
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Each key should allow 10 and block 5
        var allowed = results.Count(r => r.Allowed);
        var blocked = results.Count(r => !r.Allowed);

        Assert.Equal(50, allowed);
        Assert.Equal(25, blocked);
    }

    [Fact]
    public async Task AllowRequestAsync_SimpleAndFast()
    {
        // Arrange
        var key = "performance-test";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Make 1000 rapid requests
        for (int i = 0; i < 1000; i++)
        {
            await _limiter.AllowRequestAsync(key);
        }
        stopwatch.Stop();

        // Assert - Should complete quickly (under 500ms for simple counter operation)
        Assert.True(stopwatch.ElapsedMilliseconds < 500);
    }
}
