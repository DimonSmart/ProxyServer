using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Composable cache service that chains two cache implementations
/// </summary>
public class ComposableCacheService(ICacheService primaryCache, ICacheService? fallbackCache = null, TimeSpan? promotionTtl = null) : ICacheService
{
    private readonly ICacheService _primaryCache = primaryCache ?? throw new ArgumentNullException(nameof(primaryCache));
    private readonly TimeSpan _promotionTtl = promotionTtl ?? TimeSpan.FromMinutes(30); // Default TTL for primary cache

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await _primaryCache.GetAsync<T>(key);
        if (value != null)
        {
            return value;
        }

        if (fallbackCache != null)
        {
            var fallbackValue = await fallbackCache.GetAsync<T>(key);
            if (fallbackValue != null)
            {
                // Promote value from fallback to primary cache using primary cache TTL
                await _primaryCache.SetAsync(key, fallbackValue, _promotionTtl);
                return fallbackValue;
            }
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
