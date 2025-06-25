using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
    string GenerateCacheKey(HttpContext context);
    bool CanCache(HttpContext context);
    CacheStatistics GetStatistics();
}
