using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace DimonSmart.ProxyServer.Controllers;

/// <summary>
/// Controller for health check and statistics endpoints
/// </summary>
[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly ProxySettings _settings;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public HealthController(ICacheService cacheService, ProxySettings settings)
    {
        _cacheService = cacheService;
        _settings = settings;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    public ActionResult<HealthStatus> GetHealth()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var uptime = DateTime.UtcNow - _startTime;

        var healthStatus = new HealthStatus
        {
            Status = "Healthy",
            Version = version,
            Uptime = uptime,
            UpstreamUrl = _settings.UpstreamUrl,
            Timestamp = DateTime.UtcNow,
            Details = new Dictionary<string, object>
            {
                ["port"] = _settings.Port,
                ["memoryCacheEnabled"] = _settings.EnableMemoryCache,
                ["diskCacheEnabled"] = _settings.EnableDiskCache,
                ["memoryCacheTtlSeconds"] = _settings.MemoryCache.TtlSeconds,
                ["diskCacheTtlSeconds"] = _settings.DiskCache.TtlSeconds,
                ["authenticationEnabled"] = _settings.AllowedCredentials.Count > 0
            }
        };

        return Ok(healthStatus);
    }

    /// <summary>
    /// Simple health check endpoint (returns just status)
    /// </summary>
    /// <returns>Simple status</returns>
    [HttpGet("ping")]
    public ActionResult<object> Ping()
    {
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
