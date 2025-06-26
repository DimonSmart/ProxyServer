using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Memory cache decorator that can wrap any other cache service
/// </summary>
public class MemoryCacheDecorator : BaseCacheService, IDisposable
{
    private readonly IMemoryCache? _memoryCache;
    private readonly ICacheService? _innerCache;
    private bool _disposed = false;

    public MemoryCacheDecorator(
        IMemoryCache? memoryCache,
        ProxySettings settings,
        ILogger<MemoryCacheDecorator> logger,
        ICacheService? innerCache = null)
        : base(settings, logger)
    {
        _memoryCache = memoryCache;
        _innerCache = innerCache;
    }

    public override async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        _logger.LogDebug("MemoryCacheDecorator: Getting key {Key}", key);

        // First check memory cache
        if (_memoryCache != null && _memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Memory cache hit: {Key}", key);
            return value;
        }

        _logger.LogDebug("Memory cache miss, checking inner cache: {Key}", key);

        // If not found in memory and we have an inner cache, check it
        if (_innerCache != null)
        {
            var innerValue = await _innerCache.GetAsync<T>(key);
            if (innerValue != null)
            {
                _logger.LogDebug("Inner cache hit, promoting to memory: {Key}", key);

                // Promote to memory cache with shorter expiration
                if (_memoryCache != null)
                {
                    var hotCacheExpiration = TimeSpan.FromMinutes(Math.Min(30, _settings.CacheDurationSeconds / 60.0));
                    var options = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(hotCacheExpiration)
                        .SetSize(1)
                        .SetPriority(CacheItemPriority.Normal);

                    _memoryCache.Set(key, innerValue, options);
                }

                return innerValue;
            }
        }

        _logger.LogDebug("Cache miss: {Key}", key);
        return null;
    }

    public override async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        _logger.LogDebug("MemoryCacheDecorator: Setting key {Key}", key);

        try
        {
            // Store in inner cache first if available
            if (_innerCache != null)
            {
                await _innerCache.SetAsync(key, value, expiration);
                _logger.LogDebug("Stored in inner cache: {Key}", key);
            }

            // Store in memory cache if enabled
            if (_memoryCache != null && _settings.EnableMemoryCache)
            {
                var memoryCacheExpiration = TimeSpan.FromMinutes(Math.Min(30, expiration.TotalMinutes));
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(memoryCacheExpiration)
                    .SetSize(1)
                    .SetPriority(CacheItemPriority.Normal);

                _memoryCache.Set(key, value, options);
                _logger.LogDebug("Stored in memory cache: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store value in cache: {Key}", key);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_innerCache is IDisposable disposableInner)
            {
                disposableInner.Dispose();
            }
            _disposed = true;
        }
    }
}
