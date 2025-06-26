namespace DimonSmart.ProxyServer;

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
