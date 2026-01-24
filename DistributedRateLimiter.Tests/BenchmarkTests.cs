using DistributedRateLimiter.RateLimiting.Algorithms;
using DistributedRateLimiter.RateLimiting.InMemory;
using DistributedRateLimiter.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xunit;

namespace DistributedRateLimiter.Tests;

/// <summary>
/// Benchmark tests comparing performance of all 4 rate limiting algorithms.
/// Shows latency per request and throughput under load.
/// </summary>
public class BenchmarkTests
{
    private readonly IOptions<RateLimiterOptions> _options;

    public BenchmarkTests()
    {
        _options = Options.Create(new RateLimiterOptions
        {
            Capacity = 100,
            RefillRate = 100,
            RefillIntervalSeconds = 60,
            EnableMetrics = false
        });
    }

    [Fact]
    public async Task BenchmarkTokenBucket_LatencyAndThroughput()
    {
        var limiter = new InMemoryTokenBucket(_options);
        const int iterations = 10000;
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            var userId = $"user-{i % 100}";
            await limiter.AllowRequestAsync(userId);
        }
        
        sw.Stop();
        
        var avgLatency = sw.Elapsed.TotalMilliseconds / iterations;
        var throughput = iterations / sw.Elapsed.TotalSeconds;
        
        // Assert reasonable performance (sub-millisecond)
        Assert.True(avgLatency < 1.0, $"Token Bucket avg latency: {avgLatency:F4}ms");
        Assert.True(throughput > 5000, $"Token Bucket throughput: {throughput:F0} req/sec");
        
        // Log for visibility
        System.Console.WriteLine($"Token Bucket: {avgLatency:F4}ms/req, {throughput:F0} req/sec");
    }

    [Fact]
    public async Task BenchmarkSlidingWindow_LatencyAndThroughput()
    {
        var limiter = new SlidingWindowLimiter(_options);
        const int iterations = 10000;
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            var userId = $"user-{i % 100}";
            await limiter.AllowRequestAsync(userId);
        }
        
        sw.Stop();
        
        var avgLatency = sw.Elapsed.TotalMilliseconds / iterations;
        var throughput = iterations / sw.Elapsed.TotalSeconds;
        
        // Sliding window might be slightly slower due to queue operations
        Assert.True(avgLatency < 2.0, $"Sliding Window avg latency: {avgLatency:F4}ms");
        Assert.True(throughput > 3000, $"Sliding Window throughput: {throughput:F0} req/sec");
        
        System.Console.WriteLine($"Sliding Window: {avgLatency:F4}ms/req, {throughput:F0} req/sec");
    }

    [Fact]
    public async Task BenchmarkLeakyBucket_LatencyAndThroughput()
    {
        var limiter = new LeakyBucketLimiter(_options);
        const int iterations = 10000;
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            var userId = $"user-{i % 100}";
            await limiter.AllowRequestAsync(userId);
        }
        
        sw.Stop();
        
        var avgLatency = sw.Elapsed.TotalMilliseconds / iterations;
        var throughput = iterations / sw.Elapsed.TotalSeconds;
        
        Assert.True(avgLatency < 1.0, $"Leaky Bucket avg latency: {avgLatency:F4}ms");
        Assert.True(throughput > 5000, $"Leaky Bucket throughput: {throughput:F0} req/sec");
        
        System.Console.WriteLine($"Leaky Bucket: {avgLatency:F4}ms/req, {throughput:F0} req/sec");
    }

    [Fact]
    public async Task BenchmarkFixedWindow_LatencyAndThroughput()
    {
        var limiter = new FixedWindowLimiter(_options);
        const int iterations = 10000;
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            var userId = $"user-{i % 100}";
            await limiter.AllowRequestAsync(userId);
        }
        
        sw.Stop();
        
        var avgLatency = sw.Elapsed.TotalMilliseconds / iterations;
        var throughput = iterations / sw.Elapsed.TotalSeconds;
        
        // Fixed window should be fastest
        Assert.True(avgLatency < 0.5, $"Fixed Window avg latency: {avgLatency:F4}ms");
        Assert.True(throughput > 10000, $"Fixed Window throughput: {throughput:F0} req/sec");
        
        System.Console.WriteLine($"Fixed Window: {avgLatency:F4}ms/req, {throughput:F0} req/sec");
    }

    [Fact]
    public async Task BenchmarkComparison_AllAlgorithmsTogether()
    {
        var algorithms = new Dictionary<string, object>
        {
            ["Token Bucket"] = new InMemoryTokenBucket(_options),
            ["Sliding Window"] = new SlidingWindowLimiter(_options),
            ["Leaky Bucket"] = new LeakyBucketLimiter(_options),
            ["Fixed Window"] = new FixedWindowLimiter(_options),
        };

        var results = new Dictionary<string, (double latency, double throughput)>();
        const int iterations = 10000;

        System.Console.WriteLine("\n========== Algorithm Performance Comparison ==========");
        System.Console.WriteLine(string.Format("{0,-20} {1,-15} {2,-20}", "Algorithm", "Latency (ms)", "Throughput (req/s)"));
        System.Console.WriteLine(new string('=', 55));

        foreach (var (name, limiterObj) in algorithms)
        {
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var userId = $"user-{i % 100}";
                
                // Dynamic dispatch using reflection since they all implement IRateLimiter
                var method = limiterObj.GetType().GetMethod("AllowRequestAsync");
                var task = (Task)method!.Invoke(limiterObj, new object[] { userId })!;
                await task;
            }
            
            sw.Stop();

            var latency = sw.Elapsed.TotalMilliseconds / iterations;
            var throughput = iterations / sw.Elapsed.TotalSeconds;
            
            results[name] = (latency, throughput);
            System.Console.WriteLine(string.Format("{0,-20} {1,-15:F4} {2,-20:F0}", name, latency, throughput));
        }

        System.Console.WriteLine(new string('=', 55));

        // Determine fastest
        var fastest = results.OrderBy(x => x.Value.latency).First();
        System.Console.WriteLine($"\n✅ Fastest: {fastest.Key} ({fastest.Value.latency:F4}ms/req)");
        
        var mostThroughput = results.OrderByDescending(x => x.Value.throughput).First();
        System.Console.WriteLine($"✅ Highest Throughput: {mostThroughput.Key} ({mostThroughput.Value.throughput:F0} req/sec)\n");
    }

    [Fact]
    public async Task BenchmarkHighLoad_1000ConcurrentUsers()
    {
        var limiter = new InMemoryTokenBucket(_options);
        const int users = 1000;
        const int requestsPerUser = 10;

        var sw = Stopwatch.StartNew();
        
        var tasks = Enumerable.Range(0, users)
            .Select(async userId =>
            {
                for (int i = 0; i < requestsPerUser; i++)
                {
                    await limiter.AllowRequestAsync($"user-{userId}");
                }
            })
            .ToList();

        await Task.WhenAll(tasks);
        sw.Stop();

        var totalRequests = users * requestsPerUser;
        var throughput = totalRequests / sw.Elapsed.TotalSeconds;
        
        // Should handle 1000 concurrent users requesting 10 times each
        Assert.True(throughput > 1000, $"Throughput under concurrent load: {throughput:F0} req/sec");
        
        System.Console.WriteLine($"\nConcurrent Load Test (1000 users, 10 req each):");
        System.Console.WriteLine($"  Total Requests: {totalRequests:N0}");
        System.Console.WriteLine($"  Total Time: {sw.ElapsedMilliseconds}ms");
        System.Console.WriteLine($"  Throughput: {throughput:F0} req/sec");
    }
}
