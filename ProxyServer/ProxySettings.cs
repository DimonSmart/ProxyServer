using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer;

public class ProxySettings
{
    public List<CredentialPair> AllowedCredentials { get; set; } = new();
    public string UpstreamUrl { get; set; } = string.Empty;
    public bool UseMemoryCache { get; set; }
    public int CacheDurationSeconds { get; set; } = 60;
    public int CacheMaxEntries { get; set; } = 1000;
    public int Port { get; set; } = 5000;
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }

    // Streaming cache configuration
    public StreamingCacheSettings StreamingCache { get; set; } = new();
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