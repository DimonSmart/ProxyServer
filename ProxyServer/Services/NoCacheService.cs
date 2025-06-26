using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// No-op cache service that doesn't cache anything (terminal cache in the chain)
/// </summary>
public class NoCacheService : BaseCacheService
{
    public NoCacheService(ProxySettings settings, ILogger<NoCacheService> logger)
        : base(settings, logger)
    {
    }

    public override Task<T?> GetAsync<T>(string key) where T : class
    {
        _logger.LogDebug("No cache service - always miss: {Key}", key);
        return Task.FromResult<T?>(null);
    }

    public override Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        _logger.LogDebug("No cache service - not storing: {Key}", key);
        return Task.CompletedTask;
    }
}
