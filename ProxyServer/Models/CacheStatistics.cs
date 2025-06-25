namespace DimonSmart.ProxyServer.Models;

/// <summary>
/// Represents cache statistics and performance metrics
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of cache hits
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Number of cache misses
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// Cache hit rate as a percentage
    /// </summary>
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;

    /// <summary>
    /// Current number of items in cache
    /// </summary>
    public int CurrentEntries { get; set; }

    /// <summary>
    /// Maximum allowed cache entries
    /// </summary>
    public int MaxEntries { get; set; }

    /// <summary>
    /// Cache usage percentage
    /// </summary>
    public double UsagePercentage => MaxEntries > 0 ? (double)CurrentEntries / MaxEntries * 100 : 0;

    /// <summary>
    /// Indicates if caching is enabled
    /// </summary>
    public bool IsEnabled { get; set; }
}
