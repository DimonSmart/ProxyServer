using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Memory cache service implementation
/// </summary>
public class MemoryCacheService : BaseCacheService, IDisposable
{
    private readonly IMemoryCache? _memoryCache;
    private bool _disposed = false;

    public MemoryCacheService(
        IMemoryCache? memoryCache,
        ProxySettings settings,
        ILogger<MemoryCacheService> logger)
        : base(settings, logger)
    {
        _memoryCache = memoryCache;
    }

    public override Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return Task.FromResult<T?>(null);

        if (_memoryCache == null)
        {
            _logger.LogDebug("Memory cache disabled - miss: {Key}", key);
            return Task.FromResult<T?>(null);
        }

        var result = _memoryCache.TryGetValue(key, out T? value) ? value : null;

        if (result != null)
        {
            _logger.LogDebug("Memory cache hit: {Key}", key);
        }
        else
        {
            _logger.LogDebug("Cache miss: {Key}", key);
        }

        return Task.FromResult(result);
    }

    public override Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed || _memoryCache == null)
        {
            _logger.LogDebug("Memory cache disabled - not storing: {Key}", key);
            return Task.CompletedTask;
        }

        try
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiration)
                .SetSize(1)
                .SetPriority(CacheItemPriority.Normal);

            _memoryCache.Set(key, value, options);
            _logger.LogDebug("Stored in memory cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store value in memory cache: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
