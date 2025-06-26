using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Memory cache service implementation
/// </summary>
public class MemoryCacheService(
    IMemoryCache? memoryCache,
    ILogger<MemoryCacheService> _logger) : ICacheService, IDisposable
{
    private bool _disposed = false;

    public Task<T?> GetAsync<T>(string key) where T : class
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

    public Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
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

    public Task ClearAsync()
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
