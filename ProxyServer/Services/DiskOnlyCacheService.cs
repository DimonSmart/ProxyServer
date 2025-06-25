using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Cache service that uses only disk storage (wrapper around IDiskCacheService to implement ICacheService)
/// </summary>
public class DiskOnlyCacheService : ICacheService
{
    private readonly IDiskCacheService _diskCache;
    private readonly ProxySettings _settings;
    private readonly ILogger<DiskOnlyCacheService> _logger;
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private readonly object _statsLock = new();

    public DiskOnlyCacheService(
        IDiskCacheService diskCache,
        ProxySettings settings,
        ILogger<DiskOnlyCacheService> logger)
    {
        _diskCache = diskCache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        lock (_statsLock)
        {
            _totalRequests++;
        }

        var result = await _diskCache.GetAsync<T>(key);

        lock (_statsLock)
        {
            if (result != null)
            {
                _cacheHits++;
            }
            else
            {
                _cacheMisses++;
            }
        }

        return result;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        await _diskCache.SetAsync(key, value, expiration);
    }

    public async Task<string> GenerateCacheKeyAsync(HttpContext context)
    {
        var cacheKey = $"{context.Request.Method}:{context.Request.Path}{context.Request.QueryString}";

        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body));
            cacheKey += ":" + Convert.ToHexString(hash);
        }

        return cacheKey;
    }

    public bool CanCache(HttpContext context)
    {
        if (!_settings.EnableDiskCache) return false;

        var isGetOrPost = context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                         context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase);

        var isNotFileUpload = string.IsNullOrEmpty(context.Request.ContentType) ||
                             !context.Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);

        return isGetOrPost && isNotFileUpload;
    }

    public CacheStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            var diskEntries = _diskCache.GetCountAsync().GetAwaiter().GetResult();

            return new CacheStatistics
            {
                TotalRequests = _totalRequests,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                CurrentEntries = diskEntries,
                MaxEntries = _settings.CacheMaxEntries,
                IsEnabled = _settings.EnableDiskCache,
                HotCacheHits = 0, // No hot cache in disk-only mode
                DiskCacheHits = _cacheHits,
                HotCacheEntries = 0,
                DiskCacheEntries = diskEntries
            };
        }
    }

    public async Task StreamCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = cachedResponse.StatusCode;

        // Copy headers
        foreach (var header in cachedResponse.Headers)
        {
            if (!IsRestrictedHeader(header.Key))
            {
                try
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
                catch
                {
                    // Ignore headers that can't be set
                }
            }
        }

        // Remove problematic headers for streaming
        context.Response.Headers.Remove("content-length");
        context.Response.Headers.Remove("transfer-encoding");

        // Check if streaming is enabled and the response was originally streamed
        if (_settings.StreamingCache.EnableStreamingCache && cachedResponse.WasStreamed)
        {
            await StreamResponseInChunks(context, cachedResponse.Body, cancellationToken);
        }
        else
        {
            // Write response as a single chunk
            await context.Response.Body.WriteAsync(cachedResponse.Body, cancellationToken);
        }
    }

    public async Task WriteCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default)
    {
        // Set response status code
        context.Response.StatusCode = cachedResponse.StatusCode;

        // Copy headers exactly as they were originally received, but exclude problematic headers
        foreach (var header in cachedResponse.Headers)
        {
            if (IsRestrictedHeader(header.Key))
                continue;

            try
            {
                context.Response.Headers[header.Key] = header.Value;
            }
            catch
            {
                // Ignore headers that can't be set due to framework restrictions
            }
        }

        // Write response body as a single chunk (preserving original behavior)
        await context.Response.Body.WriteAsync(cachedResponse.Body, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    private async Task StreamResponseInChunks(HttpContext context, byte[] body, CancellationToken cancellationToken)
    {
        var chunkSize = _settings.StreamingCache.ChunkSize;
        var delayMs = _settings.StreamingCache.ChunkDelayMs;
        var totalLength = body.Length;
        var offset = 0;

        while (offset < totalLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentChunkSize = Math.Min(chunkSize, totalLength - offset);
            var chunk = new byte[currentChunkSize];
            Array.Copy(body, offset, chunk, 0, currentChunkSize);

            await context.Response.Body.WriteAsync(chunk, 0, currentChunkSize, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            offset += currentChunkSize;

            // Add delay between chunks if specified and not the last chunk
            if (delayMs > 0 && offset < totalLength)
            {
                try
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static bool IsRestrictedHeader(string headerName)
    {
        var restrictedHeaders = new[]
        {
            "connection", "content-length", "transfer-encoding", "upgrade",
            "date", "server", "via", "warning", "age", "etag", "expires",
            "last-modified", "cache-control", "vary"
        };

        return restrictedHeaders.Contains(headerName.ToLowerInvariant());
    }
}
