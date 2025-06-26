namespace DimonSmart.ProxyServer;

public class DiskCacheSettings
{
    /// <summary>
    /// Path to the disk cache database file
    /// </summary>
    public string CachePath { get; set; } = "./cache/proxy_cache.db";

    /// <summary>
    /// TTL for disk cache in seconds (default: 7 days)
    /// </summary>
    public int TtlSeconds { get; set; } = 604800; // 7 days

    /// <summary>
    /// Maximum disk cache size in MB
    /// </summary>
    public long MaxSizeMB { get; set; } = 1024; // 1GB default

    /// <summary>
    /// Interval in minutes for cleanup of expired entries
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}