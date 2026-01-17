using DistributedRateLimiter.RateLimiting.InMemory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistributedRateLimiter.Tests;

public class InMemoryTokenBucketTests
{
    private readonly Mock<ILogger<InMemoryTokenBucket>> _loggerMock;
    private readonly InMemoryTokenBucket _rateLimiter;

    public InMemoryTokenBucketTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryTokenBucket>>();
        _rateLimiter = new InMemoryTokenBucket(_loggerMock.Object);
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
