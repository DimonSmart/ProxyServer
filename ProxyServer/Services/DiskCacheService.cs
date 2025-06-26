using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Disk cache service implementation
/// </summary>
public class DiskCacheService : BaseCacheService, IDisposable
{
    private readonly IDiskCacheService _diskCache;
    private bool _disposed = false;

    public DiskCacheService(
        IDiskCacheService diskCache,
        ProxySettings settings,
        ILogger<DiskCacheService> logger)
        : base(settings, logger)
    {
        _diskCache = diskCache;
    }

    public override async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        var result = await _diskCache.GetAsync<T>(key);

        if (result != null)
        {
            _logger.LogDebug("Disk cache hit: {Key}", key);
        }
        else
        {
            _logger.LogDebug("Cache miss: {Key}", key);
        }

        return result;
    }

    public override async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        try
        {
            await _diskCache.SetAsync(key, value, expiration);
            _logger.LogDebug("Stored in disk cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store value in disk cache: {Key}", key);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_diskCache is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _disposed = true;
        }
    }
}
