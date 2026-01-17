Distributed API Rate Limiter

A Redis-backed API rate limiter built in .NET 8 with C#, demonstrating per-IP dynamic token bucket throttling. Tracks allowed vs blocked requests in Redis and is ready for scaling across multiple nodes (future enhancement).

Features (Current State)

Per-IP Rate Limiting – Each request is tracked by remote IP address

Token Bucket Algorithm – Controls request bursts efficiently

Redis Integration – Stores token counts and last refill time

Swagger/OpenAPI Ready – Test endpoints easily

Metrics – Tracks allowed vs blocked requests per IP in Redis

Tech Stack

.NET 8

C#

Redis (StackExchange.Redis)

Middleware + Endpoint Routing

Swagger / OpenAPI

Getting Started

Clone the repository

git clone https://github.com/RishabhDevDogra/distributed-rate-limiter.git
cd distributed-rate-limiter


Install dependencies

dotnet restore


Run Redis locally

brew services start redis
redis-cli ping  # should respond PONG


Run the project

dotnet run


Test the rate-limited endpoint

curl -X GET http://localhost:5126/api/limited
# Response: "Request allowed 🚀" or "Rate limit exceeded"

Current Limitations / Next Steps

Currently single-node Redis — multi-node atomic operations not implemented yet (Work in Progress)

Fallback in-memory limiter not yet active

AI-based spike detection not implemented

Frontend/dashboard for metrics not built yet
