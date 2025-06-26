using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Base class for cache services that support chaining with another cache service
/// </summary>
public abstract class ChainedCacheService(ICacheService? innerCache = null) : ICacheService
{
    protected abstract Task<T?> GetImplementationAsync<T>(string key) where T : class;

    protected abstract Task SetImplementationAsync<T>(string key, T value, TimeSpan expiration) where T : class;

    protected abstract Task ClearImplementationAsync();


    protected virtual Task OnValuePromotedAsync<T>(string key, T value) where T : class
    {
        return Task.CompletedTask;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await GetImplementationAsync<T>(key);
        if (value != null)
        {
            return value;
        }

        if (innerCache != null)
        {
            var innerValue = await innerCache.GetAsync<T>(key);
            if (innerValue != null)
            {
                await OnValuePromotedAsync(key, innerValue);
                return innerValue;
            }
        }

        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        await SetImplementationAsync(key, value, expiration);

        if (innerCache != null)
        {
            await innerCache.SetAsync(key, value, expiration);
        }
    }

    public async Task ClearAsync()
    {
        await ClearImplementationAsync();

        if (innerCache != null)
        {
            await innerCache.ClearAsync();
        }
    }
}
