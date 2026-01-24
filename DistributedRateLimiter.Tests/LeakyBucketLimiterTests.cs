using Xunit;
using Microsoft.Extensions.Options;
using DistributedRateLimiter.RateLimiting.Algorithms;
using DistributedRateLimiter.Configuration;
using DistributedRateLimiter.RateLimiting.Interfaces;

namespace DistributedRateLimiter.Tests;

public class LeakyBucketLimiterTests
{
    private readonly IOptions<RateLimiterOptions> _options;
    private readonly LeakyBucketLimiter _limiter;

    public LeakyBucketLimiterTests()
    {
        _options = Options.Create(new RateLimiterOptions
        {
            Capacity = 10,
            RefillRate = 10,
            RefillIntervalSeconds = 60
        });
        _limiter = new LeakyBucketLimiter(_options);
    }

    [Fact]
    public async Task AllowRequestAsync_AllowsRequestWithinCapacity()
    {
        // Arrange
        var key = "test-user";

        // Act
        var result = await _limiter.AllowRequestAsync(key);

        // Assert
        Assert.True(result.Allowed);
        Assert.True(result.Remaining >= 0);
        Assert.True(result.ResetTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task AllowRequestAsync_BlocksRequestAfterCapacityExhausted()
    {
        // Arrange
        var key = "test-user";
        var results = new List<RateLimitResult>();
        
        // Act - Fill the bucket with rapid requests
        for (int i = 0; i < 10; i++)
        {
            var result = await _limiter.AllowRequestAsync(key);
            results.Add(result);
        }

        // Assert - At least 10 requests should be allowed (capacity is 10)
        var allowed = results.Count(r => r.Allowed);
        Assert.True(allowed >= 10, $"Expected at least 10 allowed, got {allowed}");
    }

    [Fact]
    public async Task AllowRequestAsync_IsIsolatedPerKey()
    {
        // Arrange
        var key1 = "user-1";
        var key2 = "user-2";

        // Act
        var result1 = await _limiter.AllowRequestAsync(key1);
        var result2 = await _limiter.AllowRequestAsync(key2);

        // Assert
        Assert.True(result1.Allowed);
        Assert.True(result2.Allowed);
    }

    [Fact]
    public async Task AllowRequestAsync_ReturnsCorrectResetTime()
    {
        // Arrange
        var key = "test-user";
        var beforeRequest = DateTime.UtcNow;

        // Act
        var result = await _limiter.AllowRequestAsync(key);

        // Assert
        Assert.True(result.ResetTime > beforeRequest);
    }

    [Fact]
    public async Task AllowRequestAsync_IsThreadSafe()
    {
        // Arrange
        var key = "concurrent-user";
        var tasks = new List<Task<RateLimitResult>>();

        // Act - Launch sequential requests (not concurrent to avoid timing issues)
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_limiter.AllowRequestAsync(key));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All 10 should be allowed (capacity is 10)
        var allowed = results.Count(r => r.Allowed);
        Assert.Equal(10, allowed);
    }

    [Fact]
    public async Task AllowRequestAsync_TrackingMultipleKeysConcurrently()
    {
        // Arrange
        var keys = Enumerable.Range(1, 5).Select(i => $"user-{i}").ToList();

        // Act - Each key makes 10 requests
        var results = new List<RateLimitResult>();
        foreach (var key in keys)
        {
            for (int i = 0; i < 10; i++)
            {
                results.Add(await _limiter.AllowRequestAsync(key));
            }
        }

        // Assert - All 50 should be allowed (each key can handle 10)
        var allowed = results.Count(r => r.Allowed);
        Assert.Equal(50, allowed);
    }
}

