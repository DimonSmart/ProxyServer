using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Interfaces;

/// <summary>
/// Extended interface for cache services that support cleanup and size monitoring
/// </summary>
public interface IExtendedCacheService : ICacheService
{
    Task CleanupExpiredAsync();
    Task<long> GetSizeAsync();
    Task<List<CacheEntry>> GetAllEntriesAsync(string? filter = null);
}
