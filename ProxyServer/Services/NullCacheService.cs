using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Null cache service that doesn't cache anything
/// </summary>
public class NullCacheService : ChainedCacheService
{
    public NullCacheService() : base()
    {
    }

    protected override Task<T?> GetImplementationAsync<T>(string key) where T : class
    {
        return Task.FromResult<T?>(null);
    }

    protected override Task SetImplementationAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        return Task.CompletedTask;
    }

    protected override Task ClearImplementationAsync()
    {
        return Task.CompletedTask;
    }
}
