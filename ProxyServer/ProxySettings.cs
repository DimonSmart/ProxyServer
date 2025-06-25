using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer;

public class ProxySettings
{
    public List<CredentialPair> AllowedCredentials { get; set; } = new();
    public string UpstreamUrl { get; set; } = string.Empty;
    public bool EnableMemoryCache { get; set; } = true;
    public bool EnableDiskCache { get; set; } = false;
    public int CacheDurationSeconds { get; set; } = 60;
    public int CacheMaxEntries { get; set; } = 1000;
    public int Port { get; set; } = 5000;
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }

    public StreamingCacheSettings StreamingCache { get; set; } = new();

    public DiskCacheSettings DiskCache { get; set; } = new();
}

public class StreamingCacheSettings
{
    /// <summary>
    /// Enable streaming cached responses that were originally streamed
    /// </summary>
    public bool EnableStreamingCache { get; set; } = false;

    /// <summary>
    /// Size of each chunk in bytes when streaming cached responses
    /// </summary>
    public int ChunkSize { get; set; } = 1024;

    /// <summary>
    /// Delay in milliseconds between chunks when streaming cached responses
    /// </summary>
    public int ChunkDelayMs { get; set; } = 10;
}

public class DiskCacheSettings
{
    /// <summary>
    /// Path to the disk cache database file
    /// </summary>
    public string CachePath { get; set; } = "./cache/proxy_cache.db";

    /// <summary>
    /// Maximum disk cache size in MB
    /// </summary>
    public long MaxSizeMB { get; set; } = 1024; // 1GB default

    /// <summary>
    /// Interval in minutes for cleanup of expired entries
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    // Removed UseHybridCache as it's now determined by EnableMemoryCache && EnableDiskCache
}