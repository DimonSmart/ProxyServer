using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Cache service that uses only disk storage (wrapper around IDiskCacheService to implement ICacheService)
/// </summary>
public class DiskOnlyCacheService : ICacheService
{
    private readonly IDiskCacheService _diskCache;

    public DiskOnlyCacheService(IDiskCacheService diskCache)
    {
        _diskCache = diskCache;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        return await _diskCache.GetAsync<T>(key);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        await _diskCache.SetAsync(key, value, expiration);
    }
}
