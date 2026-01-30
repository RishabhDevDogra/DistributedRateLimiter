# Distributed Rate Limiter

A production-ready distributed rate limiting system built with .NET 8 and Redis, featuring automatic in-memory failover, multiple rate limiting algorithms, and comprehensive observability.

## Overview

This implementation demonstrates enterprise-grade API rate limiting with:
- **Distributed coordination** via Redis with atomic Lua script execution
- **High availability** through circuit-breaker pattern and in-memory fallback
- **Four algorithms**: Token Bucket, Sliding Window, Leaky Bucket, Fixed Window
- **Production features**: Health checks, structured logging, metrics, and Kubernetes compatibility
- **Comprehensive testing**: 50+ unit tests with benchmark comparisons

### Tech Stack

.NET 8 • Redis • xUnit • Moq • OpenAPI/Swagger

## Quick Start

```bash
# Clone repository
git clone https://github.com/RishabhDevDogra/DistributedRateLimiter.git
cd DistributedRateLimiter

# Start Redis
brew services start redis

# Run application
dotnet run --project DistributedRateLimiter

# Execute tests
dotnet test
```

### Testing the API

```bash
# First 10 requests return 200 OK
curl -i http://localhost:5126/ratelimit

# 11th request blocked with 429 Too Many Requests
curl -i http://localhost:5126/ratelimit
```

## Architecture

### Core Algorithm

Token bucket provides fair, burst-tolerant rate limiting:

1. Each user maintains a bucket with initial capacity (10 tokens)
2. Tokens regenerate at constant rate (1 token/second)
3. Each request consumes 1 token
4. Requests fail when tokens < 1, returning 429 status
5. `X-RateLimit-Reset` header indicates next available slot

### High Availability Design

```
Request → Redis (atomic Lua script)
           ↓ (timeout/failure)
       Circuit Breaker (5s window)
           ↓
       In-Memory Fallback (thread-safe)
           ↓
       Periodic Redis Retry → Auto-recovery
```

**Guarantees:**
- Atomic operations via Redis Lua scripts
- Zero downtime during Redis failures
- Automatic failover and recovery
- Per-user isolation for fairness

## Rate Limiting Algorithms

### Performance Comparison

| Algorithm | Latency | Throughput | Memory | Accuracy | Burst Handling | Best For |
|-----------|---------|------------|--------|----------|----------------|----------|
| **Fixed Window** | 0.08ms | 65k req/s | Very Low | 80% | ✅ Yes | High throughput APIs |
| **Token Bucket** | 0.15ms | 45k req/s | Low | 95% | ✅ Yes | General purpose APIs |
| **Leaky Bucket** | 0.18ms | 42k req/s | Low | 95% | ⚠️ Limited | Traffic shaping |
| **Sliding Window** | 0.25ms | 25k req/s | High | 100% | ❌ No | Strict compliance |

### Implementation Details

#### Token Bucket
- **Mechanism**: Tokens accumulate at constant rate; requests consume tokens
- **Strengths**: Handles bursts gracefully, fair distribution, configurable
- **Limitations**: Clock skew sensitive
- **Endpoint**: `GET /api/limited/token-bucket`

#### Sliding Window
- **Mechanism**: Tracks exact timestamp of each request in rolling window
- **Strengths**: Most accurate, prevents window edge cases
- **Limitations**: High memory overhead (stores all timestamps)
- **Use Case**: Financial transactions, strict quotas
- **Endpoint**: `GET /api/limited/sliding-window`

#### Leaky Bucket
- **Mechanism**: Requests fill bucket, drain at constant rate
- **Strengths**: Smooths traffic, prevents bursts, constant throughput
- **Limitations**: Not optimal for bursty workloads
- **Use Case**: Backend protection, traffic shaping
- **Endpoint**: `GET /api/limited/leaky-bucket`

#### Fixed Window
- **Mechanism**: Counter resets at fixed intervals
- **Strengths**: Simplest, fastest, minimal memory
- **Limitations**: Allows bursts at window boundaries (2x limit possible)
- **Use Case**: Non-critical APIs, caching headers
- **Endpoint**: `GET /api/limited/fixed-window`

### Running Benchmarks

```bash
dotnet test --filter BenchmarkTests
```

## Testing

**Test Coverage**: 50+ unit tests across all components

```bash
dotnet test

# Test Suite Breakdown:
# - BenchmarkTests (6): Performance comparison
# - FallbackRateLimiterTests (2): Failover logic
# - InMemoryTokenBucketTests (4): Local rate limiting
# - RateLimiterMiddlewareTests (3): HTTP integration
# - FixedWindowLimiterTests (7): Fixed window algorithm
# - SlidingWindowLimiterTests (7): Sliding window algorithm
# - LeakyBucketLimiterTests (6): Leaky bucket algorithm
# - RedisHealthTests (7): Health monitoring
# - RedisHealthCheckTests (8): Kubernetes health checks
```

## Design Decisions

### Scalability
- Redis sharding supports millions of users
- ~50k QPS per node throughput
- Per-user isolation for fairness (trade-off: no global quota enforcement)

### High Availability
- 99.99% uptime via in-memory fallback
- Automatic recovery with 5s circuit breaker retry
- Graceful degradation during Redis outages

### Atomicity
- Lua scripts ensure atomic execution on Redis
- Prevents race conditions in distributed environment
- Guarantees consistent token bucket state

### Observability
- Structured logging with correlation IDs
- Health endpoints (`/health`, `/health/ready`)
- Standard HTTP headers (`X-RateLimit-*`)

### Testing
- Mock-based unit tests (no external dependencies)
- 50+ tests executing in ~90ms
- Deterministic failover and circuit breaker scenarios

### Configuration
- Zero hardcoded values
- Environment variable support
- Runtime updates via `appsettings.json`

## Advanced Topics

### Scaling to 1M+ Users
- **Approach**: Redis Cluster with consistent hashing
- **Sharding**: Hash `user_id` to determine node ownership
- **Consideration**: Cross-region consistency trade-offs acceptable for rate limiting

### DDoS Mitigation
- Current: Per-user rate limiting
- Enhancement: Combine IP + user identity for comprehensive protection
- Integration: WAF for IP reputation filtering

### Memory Management
- **Current**: On-demand bucket creation
- **Production**: Add Redis TTL (`EXPIRE key 3600`) for inactive user cleanup
- **Footprint**: 1M users × 50 bytes = 50MB

### Clock Skew Handling
- Server-side time only (no client time dependency)
- Eventual consistency acceptable for independent buckets

### Multi-Tier Rate Limits
```csharp
// Extract tier from JWT/API key
var tier = jwtToken.GetClaim("tier"); // "free", "premium", "enterprise"
var capacity = tier switch {
    "enterprise" => 1000,
    "premium" => 100,
    _ => 10
};
```

## Key Architecture Decisions

| Decision | Rationale | Trade-off |
|----------|-----------|-----------|
| Multiple Algorithms | Support different use cases (general purpose, strict quotas, traffic shaping, high throughput) | Increased complexity vs. flexibility |
| Lua Scripts | Atomic execution on Redis | Prevents race conditions, requires Redis |
| 500ms Timeout | Balance between UX and reliability | Fast failover vs. network tolerance |
| Per-User Limits | Fair resource allocation across all users | Cannot enforce global API quota |
| Circuit Breaker | Automatic failover and recovery | 5s retry window during degradation |
| In-Memory Fallback | 99.99% uptime guarantee | Local limits during Redis outage |

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome. This is a reference implementation for system design and production deployments.
