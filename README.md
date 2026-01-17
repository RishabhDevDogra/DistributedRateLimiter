# Distributed API Rate Limiter

**.NET 8 | Redis | Token Bucket | Per-IP Throttling**

A **Redis-backed API rate limiter** built in **.NET 8** that throttles requests per **remote IP** using the **token bucket algorithm**. Tracks **allowed vs blocked requests** in Redis and is ready for future multi-node scaling.

---

## 🌟 Features

- **Per-IP Rate Limiting** – Tracks requests per client IP
- **Token Bucket Algorithm** – Controls request bursts efficiently
- **Redis Integration** – Stores token counts & last refill timestamps
- **Swagger/OpenAPI** – Easily test endpoints
- **Metrics** – Tracks allowed vs blocked requests in real-time

---

## 🛠 Tech Stack

- **.NET 8 / C#**
- **Redis (StackExchange.Redis)**
- **Middleware + Endpoint Routing**
- **Swagger / OpenAPI**
## ⚠️ Limitations
- **Currently single-node Redis (multi-node atomic ops not implemented)**

- **Fallback in-memory limiter not active**

- **AI-based spike detection not implemented**

Frontend/dashboard for metrics not built
---

## ⚡ Getting Started



```bash
git clone https://github.com/RishabhDevDogra/distributed-rate-limiter.git
cd distributed-rate-limiter
Restore dependencies

dotnet restore
Start Redis locally

brew services start redis
redis-cli ping  # should respond PONG
Run the project

dotnet run
Test the rate-limited endpoint

curl -X GET http://localhost:5126/api/limited
# Response: "Request allowed 🚀" or "Rate limit exceeded"


