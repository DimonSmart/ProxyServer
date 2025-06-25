using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
    Task<string> GenerateCacheKeyAsync(HttpContext context);
    bool CanCache(HttpContext context);
    CacheStatistics GetStatistics();
    Task StreamCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default);
    Task WriteCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default);
}
