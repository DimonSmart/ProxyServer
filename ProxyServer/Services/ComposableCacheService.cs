using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Composable cache service that chains two cache implementations
/// </summary>
public class ComposableCacheService(ICacheService primaryCache, ICacheService? fallbackCache = null) : ICacheService
{
    private readonly ICacheService _primaryCache = primaryCache ?? throw new ArgumentNullException(nameof(primaryCache));

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await _primaryCache.GetAsync<T>(key);
        if (value != null)
        {
            return value;
        }

        if (fallbackCache != null)
        {
            return await fallbackCache.GetAsync<T>(key);
        }

        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        // Set in primary cache
        await _primaryCache.SetAsync(key, value, expiration);

        // Also set in fallback cache if available
        if (fallbackCache != null)
        {
            await fallbackCache.SetAsync(key, value, expiration);
        }
    }

    public async Task ClearAsync()
    {
        // Clear primary cache
        await _primaryCache.ClearAsync();

        // Clear fallback cache if available
        if (fallbackCache != null)
        {
            await fallbackCache.ClearAsync();
        }
    }
}
