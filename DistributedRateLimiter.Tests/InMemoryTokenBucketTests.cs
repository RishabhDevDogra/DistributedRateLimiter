using DistributedRateLimiter.RateLimiting.InMemory;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class InMemoryTokenBucketTests
{
    private readonly InMemoryTokenBucket _rateLimiter;

    public InMemoryTokenBucketTests()
    {
        var options = Options.Create(new RateLimiterOptions
        {
            Capacity = 10,
            RefillRate = 10,
            RefillIntervalSeconds = 60
        });
        _rateLimiter = new InMemoryTokenBucket(options);
    }

    [Fact]
    public async Task FirstRequest_ShouldBeAllowed()
    {
        var result = await _rateLimiter.AllowRequestAsync("test-user");
        Assert.True(result.Allowed);
        Assert.Equal(9, result.Remaining);
    }

    [Fact]
    public async Task MultipleRequests_ShouldDecrementRemaining()
    {
        var key = "test-user";
        var result1 = await _rateLimiter.AllowRequestAsync(key);
        var result2 = await _rateLimiter.AllowRequestAsync(key);
        
        Assert.Equal(9, result1.Remaining);
        Assert.Equal(8, result2.Remaining);
    }

    [Fact]
    public async Task ExceedCapacity_ShouldBlock()
    {
        var key = "test-user";
        for (int i = 0; i < 10; i++)
        {
            await _rateLimiter.AllowRequestAsync(key);
        }
        
        var result = await _rateLimiter.AllowRequestAsync(key);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task DifferentKeys_ShouldHaveSeparateLimits()
    {
        var result1 = await _rateLimiter.AllowRequestAsync("user-1");
        var result2 = await _rateLimiter.AllowRequestAsync("user-2");
        
        Assert.Equal(9, result1.Remaining);
        Assert.Equal(9, result2.Remaining);
    }
}
