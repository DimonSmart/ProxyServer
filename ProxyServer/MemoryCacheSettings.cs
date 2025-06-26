namespace DimonSmart.ProxyServer;

/// <summary>
/// Settings for in-memory cache
/// </summary>
public class MemoryCacheSettings
{
    /// <summary>
    /// TTL for memory cache in seconds (default: 30 minutes)
    /// </summary>
    public int TtlSeconds { get; set; } = 1800; // 30 minutes

    /// <summary>
    /// Maximum number of entries in memory cache
    /// </summary>
    public int MaxEntries { get; set; } = 1000;
}
