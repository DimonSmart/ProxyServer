using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Memory cache service implementation with optional inner cache composition
/// </summary>
public class MemoryCacheService(
    IMemoryCache? memoryCache,
    ILogger<MemoryCacheService> _logger,
    ICacheService? innerCache = null) : ChainedCacheService(innerCache), IDisposable
{
    private bool _disposed = false;

    protected override Task<T?> GetImplementationAsync<T>(string key) where T : class
    {
        if (_disposed) return Task.FromResult<T?>(null);

        if (memoryCache != null && memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Memory cache hit: {Key}", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("Memory cache miss: {Key}", key);
        return Task.FromResult<T?>(null);
    }

    protected override Task SetImplementationAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return Task.CompletedTask;

        if (memoryCache != null)
        {
            try
            {
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(expiration)
                    .SetSize(1)
                    .SetPriority(CacheItemPriority.Normal);

                memoryCache.Set(key, value, options);
                _logger.LogDebug("Stored in memory cache: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store value in memory cache: {Key}", key);
            }
        }

        return Task.CompletedTask;
    }

    protected override Task ClearImplementationAsync()
    {
        if (_disposed) return Task.CompletedTask;

        if (memoryCache != null)
        {
            try
            {
                _logger.LogWarning("Memory cache clear requested, but MemoryCache doesn't support clearing. Consider restarting the application.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cache clear attempt");
            }
        }

        return Task.CompletedTask;
    }

    protected override Task OnValuePromotedAsync<T>(string key, T value) where T : class
    {
        _logger.LogDebug("Promoting value from disk cache to memory: {Key}", key);

        if (memoryCache != null)
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)) // Shorter expiration for promoted items
                .SetSize(1)
                .SetPriority(CacheItemPriority.Normal);

            memoryCache.Set(key, value, options);
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
