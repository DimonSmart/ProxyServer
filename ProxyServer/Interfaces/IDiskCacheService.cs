namespace DimonSmart.ProxyServer.Interfaces;

public interface IDiskCacheService : IDisposable
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
    Task<bool> ContainsKeyAsync(string key);
    Task RemoveAsync(string key);
    Task CleanupExpiredAsync();
    Task<long> GetSizeAsync();
    Task<int> GetCountAsync();
    Task ClearAsync();
}
