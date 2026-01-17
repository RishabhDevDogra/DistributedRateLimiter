using DistributedRateLimiter.RateLimiting.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DistributedRateLimiter.Controllers;

[ApiController]
[Route("api/limited")]
public class RateLimitedController : ControllerBase
{
    private readonly IRateLimiter _rateLimiter;

    public RateLimitedController(IRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var key = "user-123"; // temporary

        var result = await _rateLimiter.AllowRequestAsync(key);

        if (!result.Allowed)
            return StatusCode(429, "Rate limit exceeded");

        return Ok("Request allowed 🚀");
    }
}
