using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Services;

public class HybridCacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache? _hotCache;
    private readonly IDiskCacheService _diskCache;
    private readonly ProxySettings _settings;
    private readonly ILogger<HybridCacheService> _logger;
    private long _totalRequests;
    private long _hotCacheHits;
    private long _diskCacheHits;
    private long _cacheMisses;
    private readonly object _statsLock = new();
    private bool _disposed = false;

    public HybridCacheService(
        IMemoryCache? hotCache,
        IDiskCacheService diskCache,
        ProxySettings settings,
        ILogger<HybridCacheService> logger)
    {
        _hotCache = hotCache;
        _diskCache = diskCache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        lock (_statsLock)
        {
            _totalRequests++;
        }

        // First check hot cache (memory)
        if (_hotCache != null && _hotCache.TryGetValue(key, out T? hotValue))
        {
            lock (_statsLock)
            {
                _hotCacheHits++;
            }
            _logger.LogDebug("Cache hit (hot): {Key}", key);
            return hotValue;
        }

        // Then check disk cache
        var diskValue = await _diskCache.GetAsync<T>(key);
        if (diskValue != null)
        {
            lock (_statsLock)
            {
                _diskCacheHits++;
            }
            _logger.LogDebug("Cache hit (disk): {Key}", key);

            // Promote to hot cache with shorter expiration
            if (_hotCache != null)
            {
                var hotCacheExpiration = TimeSpan.FromMinutes(Math.Min(30, _settings.CacheDurationSeconds / 60.0));
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(hotCacheExpiration)
                    .SetSize(1)
                    .SetPriority(CacheItemPriority.Normal);

                _hotCache.Set(key, diskValue, options);
                _logger.LogDebug("Promoted to hot cache: {Key}", key);
            }

            return diskValue;
        }

        // Cache miss
        lock (_statsLock)
        {
            _cacheMisses++;
        }
        _logger.LogDebug("Cache miss: {Key}", key);
        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        try
        {
            // Always store in disk cache for persistence
            await _diskCache.SetAsync(key, value, expiration);
            _logger.LogDebug("Stored in disk cache: {Key}", key);

            // Store in hot cache with shorter expiration if memory cache is enabled
            if (_hotCache != null && _settings.EnableMemoryCache)
            {
                var hotCacheExpiration = TimeSpan.FromMinutes(Math.Min(30, expiration.TotalMinutes));
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(hotCacheExpiration)
                    .SetSize(1)
                    .SetPriority(CacheItemPriority.High); // New items get high priority

                _hotCache.Set(key, value, options);
                _logger.LogDebug("Stored in hot cache: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key {Key}", key);
        }
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

            // Use a more efficient hash for large bodies
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body));
            cacheKey += ":" + Convert.ToHexString(hash);
        }

        return cacheKey;
    }

    public bool CanCache(HttpContext context)
    {
        // Check if either hot or disk cache is enabled
        var canUseHotCache = _settings.EnableMemoryCache && _hotCache != null;
        var canUseDiskCache = _diskCache != null;

        if (!canUseHotCache && !canUseDiskCache) return false;

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
            var currentHotEntries = 0;
            if (_hotCache is MemoryCache mc)
            {
                // Try to get current entry count using reflection
                var field = typeof(MemoryCache).GetField("_coherentState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(mc) is object coherentState)
                {
                    var countProp = coherentState.GetType().GetProperty("Count");
                    if (countProp != null)
                    {
                        currentHotEntries = (int)(countProp.GetValue(coherentState) ?? 0);
                    }
                }
            }

            // Get disk cache statistics asynchronously
            var diskEntries = _diskCache.GetCountAsync().GetAwaiter().GetResult();

            return new CacheStatistics
            {
                TotalRequests = _totalRequests,
                CacheHits = _hotCacheHits + _diskCacheHits,
                CacheMisses = _cacheMisses,
                CurrentEntries = currentHotEntries + diskEntries,
                MaxEntries = _settings.CacheMaxEntries,
                IsEnabled = (_settings.EnableMemoryCache && _hotCache != null) || _diskCache != null,
                // Additional hybrid cache stats
                HotCacheHits = _hotCacheHits,
                DiskCacheHits = _diskCacheHits,
                HotCacheEntries = currentHotEntries,
                DiskCacheEntries = diskEntries
            };
        }
    }

    public async Task StreamCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default)
    {
        // Set response status code
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
            // Skip headers that would cause issues with cached responses
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _diskCache?.Dispose();
            _disposed = true;
        }
    }
}
