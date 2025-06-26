using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Base cache service implementation
/// </summary>
public abstract class BaseCacheService : ICacheService
{
    protected readonly ProxySettings _settings;
    protected readonly ILogger _logger;

    protected BaseCacheService(ProxySettings settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public abstract Task<T?> GetAsync<T>(string key) where T : class;
    public abstract Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
    public abstract Task ClearAsync();
}
