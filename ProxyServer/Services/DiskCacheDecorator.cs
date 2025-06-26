using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Disk cache decorator that can wrap any other cache service
/// </summary>
public class DiskCacheDecorator : BaseCacheService, IDisposable
{
    private readonly IDiskCacheService _diskCache;
    private readonly ICacheService? _innerCache;
    private bool _disposed = false;

    public DiskCacheDecorator(
        IDiskCacheService diskCache,
        ProxySettings settings,
        ILogger<DiskCacheDecorator> logger,
        ICacheService? innerCache = null)
        : base(settings, logger)
    {
        _diskCache = diskCache;
        _innerCache = innerCache;
    }

    public override async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        // First check inner cache if available (usually memory cache)
        if (_innerCache != null)
        {
            var innerValue = await _innerCache.GetAsync<T>(key);
            if (innerValue != null)
            {
                _logger.LogDebug("Inner cache hit: {Key}", key);
                return innerValue;
            }
        }

        // Then check disk cache
        var diskValue = await _diskCache.GetAsync<T>(key);
        if (diskValue != null)
        {
            _logger.LogDebug("Disk cache hit: {Key}", key);
            return diskValue;
        }

        _logger.LogDebug("Cache miss: {Key}", key);
        return null;
    }

    public override async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        try
        {
            // Store in inner cache first if available
            if (_innerCache != null)
            {
                await _innerCache.SetAsync(key, value, expiration);
                _logger.LogDebug("Stored in inner cache: {Key}", key);
            }

            // Always store in disk cache for persistence
            await _diskCache.SetAsync(key, value, expiration);
            _logger.LogDebug("Stored in disk cache: {Key}", key);
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

            if (_diskCache is IDisposable disposableDisk)
            {
                disposableDisk.Dispose();
            }

            _disposed = true;
        }
    }
}
