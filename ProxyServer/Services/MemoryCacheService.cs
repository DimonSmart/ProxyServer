using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Memory cache service implementation with optional inner cache composition
/// </summary>
public class MemoryCacheService(
    IMemoryCache? memoryCache,
    ProxySettings settings,
    ILogger<MemoryCacheService> logger,
    ICacheService? innerCache = null) : BaseCacheService(settings, logger), IDisposable
{
    private bool _disposed = false;

    public override async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        // First check memory cache
        if (memoryCache != null && memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Memory cache hit: {Key}", key);
            return value;
        }

        _logger.LogDebug("Memory cache miss: {Key}", key);

        // If not found in memory and we have an inner cache, check it
        if (innerCache != null)
        {
            var innerValue = await innerCache.GetAsync<T>(key);
            if (innerValue != null)
            {
                _logger.LogDebug("Inner cache hit, promoting to memory: {Key}", key);

                // Promote to memory cache with shorter expiration
                if (memoryCache != null)
                {
                    var options = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)) // Shorter expiration for promoted items
                        .SetSize(1)
                        .SetPriority(CacheItemPriority.Normal);

                    memoryCache.Set(key, innerValue, options);
                }

                return innerValue;
            }
        }

        return null;
    }

    public override async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        // Store in memory cache if available
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

        // Also store in inner cache if available
        if (innerCache != null)
        {
            await innerCache.SetAsync(key, value, expiration);
        }
    }

    public override async Task ClearAsync()
    {
        if (_disposed) return;

        // Clear memory cache
        if (memoryCache != null)
        {
            try
            {
                // Memory cache doesn't have a clear method, dispose and recreate is not possible here
                // Log warning as memory cache clearing is not fully supported
                _logger.LogWarning("Memory cache clear requested, but MemoryCache doesn't support clearing. Consider restarting the application.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cache clear attempt");
            }
        }

        // Clear inner cache if available
        if (innerCache != null)
        {
            await innerCache.ClearAsync();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
