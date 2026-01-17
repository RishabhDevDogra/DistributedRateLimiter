using DistributedRateLimiter.RateLimiting.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace DistributedRateLimiter.RateLimiting.Redis;

public class RedisTokenBucket : IRateLimiter
{
    private readonly IDatabase _db;

    private const int Capacity = 10;
    private const double RefillRatePerSecond = 1;

    public RedisTokenBucket(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<bool> AllowRequestAsync(string key)
{
    var redisKey = $"rate:{key}";
    var now = DateTime.UtcNow;

    var data = await _db.StringGetAsync(redisKey);

    TokenBucketState state;

    if (data.IsNullOrEmpty)
    {
        state = new TokenBucketState
        {
            Tokens = Capacity - 1,
            LastRefill = now
        };
    }
    else
    {
        state = JsonSerializer.Deserialize<TokenBucketState>(data!)!;

        var elapsedSeconds = (now - state.LastRefill).TotalSeconds;
        var refill = elapsedSeconds * RefillRatePerSecond;

        state.Tokens = Math.Min(Capacity, state.Tokens + refill);
        state.LastRefill = now;

        if (state.Tokens < 1)
        {
            await _db.StringSetAsync(redisKey, JsonSerializer.Serialize(state));
            
            // LOGGING
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now}] {redisKey} -> Tokens={state.Tokens:F2} ❌ Blocked");
            Console.ResetColor();

            return false;
        }

        state.Tokens -= 1;
    }

    await _db.StringSetAsync(redisKey, JsonSerializer.Serialize(state));

    // LOGGING
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[{DateTime.Now}] {redisKey} -> Tokens={state.Tokens:F2} ✅ Allowed");
    Console.ResetColor();

    return true;
}


    private class TokenBucketState
    {
        public double Tokens { get; set; }
        public DateTime LastRefill { get; set; }
    }
}
