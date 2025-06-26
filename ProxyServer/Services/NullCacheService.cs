using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Null cache service that doesn't cache anything
/// </summary>
public class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key) where T : class
    {
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        return Task.CompletedTask;
    }
}
