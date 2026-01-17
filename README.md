# Distributed Rate Limiter

**.NET 8 | Redis + In-Memory Fallback | Token Bucket Algorithm | Production-Ready**

A **distributed API rate limiter** built in **.NET 8** using the **token bucket algorithm** with **Redis integration** and **automatic in-memory fallback**. Implements fast failover, HTTP standard headers, and comprehensive unit tests.

---

## 🌟 Features

✅ **Token Bucket Algorithm** – 10-token capacity, 1 token/sec refill rate  
✅ **Redis-Backed** – Atomic Lua scripts for thread-safe operations  
✅ **Automatic Failover** – Seamless fallback to in-memory when Redis unavailable  
✅ **Fast Failover** – 500ms timeout for quick degradation  
✅ **Per-User Isolation** – Tracks limits per unique user identifier  
✅ **HTTP Standard Headers** – `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`  
✅ **429 Too Many Requests** – Standard HTTP response when rate limited  
✅ **Comprehensive Tests** – 10 unit tests with xUnit + Moq (100% passing)  
✅ **Production Logging** – Detailed error and failover tracking  

---

## 🛠 Tech Stack

- **.NET 8 / C#** – Modern async/await patterns
- **Redis (StackExchange.Redis)** – Distributed caching with Lua scripts
- **xUnit + Moq** – Comprehensive unit test coverage
- **ASP.NET Core Middleware** – Request filtering and header injection
- **Dependency Injection** – Clean, testable architecture

---

## 📐 Architecture

### Token Bucket Algorithm
```
Capacity: 10 tokens
Refill Rate: 1 token/second
Response: Block (429) when tokens exhausted
Reset Time: Automatically calculated per user
```

### Request Flow
```
Request → Middleware → FallbackRateLimiter
                         ├→ Try Redis (500ms timeout)
                         │  └→ On success: return RateLimitResult
                         └→ On Redis failure: Use InMemory fallback
                            └→ return RateLimitResult
             ↓
    Add X-RateLimit-* headers
             ↓
    Allow (200) or Block (429)
```

---

## 🚀 Quick Start

### Setup

```bash
# Clone and setup
git clone https://github.com/RishabhDevDogra/DistributedRateLimiter.git
cd DistributedRateLimiter

# Start Redis (macOS)
brew services start redis
redis-cli ping

# Run application
dotnet run --project DistributedRateLimiter

# Run tests
dotnet test DistributedRateLimiter.Tests
```

---

## 📊 API Usage

```bash
# Allowed (first 10 requests)
curl -i http://localhost:5126/ratelimit
# 200 OK with X-RateLimit-* headers

# Blocked (11th request)
curl -i http://localhost:5126/ratelimit
# 429 Too Many Requests
```

---

## ✅ Test Coverage

**10 Unit Tests (100% pass rate)**
- InMemoryTokenBucketTests (4 tests) – Token bucket logic
- FallbackRateLimiterTests (3 tests) – Redis failover behavior  
- RateLimiterMiddlewareTests (3 tests) – HTTP headers & blocking

Run tests: `dotnet test DistributedRateLimiter.Tests`

---

## 🔍 Design Decisions

**Why Token Bucket?** Industry standard (AWS, Stripe, GitHub)  
**Why Redis + In-Memory?** Distributed + resilient with automatic failover  
**Why 500ms Timeout?** Fast enough for imperceptible user impact  
**Why Lua Scripts?** Atomic operations prevent race conditions

---

## 🎯 Interview Discussion Points

1. Token bucket algorithm & parameters
2. Distributed vs in-memory rate limiting
3. Failover strategy & resilience patterns
4. Atomic operations with Lua scripts
5. Unit testing with mocks
6. Production logging & observability
7. Scalability to 100k+ users
8. Edge cases (clock skew, persistence)

---

## 📝 License

MIT – Feel free to use for learning and projects

