using DimonSmart.ProxyServer.Models;
using System.Text.Json;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Service for creating cache dumps in different formats
/// </summary>
public class CacheDumpService
{
    private readonly ILogger<CacheDumpService> _logger;

    public CacheDumpService(ILogger<CacheDumpService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a basic cache dump with base64 encoded data
    /// </summary>
    public CacheDump CreateBasicDump(List<CacheEntry> entries, string? filter = null)
    {
        return new CacheDump
        {
            DumpTimestamp = DateTimeOffset.UtcNow,
            TotalEntries = entries.Count,
            FilteredEntries = entries.Count,
            Filter = filter,
            Entries = entries
        };
    }

    /// <summary>
    /// Creates a detailed cache dump with decoded data from base64 cache entries
    /// </summary>
    public DetailedCacheDump CreateDetailedDump(List<CacheEntry> entries, string? filter = null)
    {
        var dump = new DetailedCacheDump
        {
            DumpTimestamp = DateTimeOffset.UtcNow,
            TotalEntries = entries.Count,
            FilteredEntries = entries.Count,
            Filter = filter,
            Statistics = new CacheDumpStatistics()
        };

        foreach (var entry in entries)
        {
            try
            {
                var detailedEntry = ConvertToDetailedEntry(entry);
                if (detailedEntry != null)
                {
                    dump.Entries.Add(detailedEntry);
                    UpdateStatistics(dump.Statistics, detailedEntry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert cache entry {Key} to detailed format", entry.Key);
            }
        }

        return dump;
    }

    private DetailedCacheEntry? ConvertToDetailedEntry(CacheEntry entry)
    {
        try
        {
            if (entry.Type != "DimonSmart.ProxyServer.Models.CachedResponse")
            {
                // For non-CachedResponse types, just return basic info
                return new DetailedCacheEntry
                {
                    CacheKeyHash = entry.Key,
                    CacheKey = "Unknown (non-HTTP cache entry)",
                    Request = new CachedRequestInfo(), // Initialize Request property
                    Metadata = new CacheMetadata
                    {
                        CreatedAt = entry.CreatedAt,
                        ExpiresAt = entry.ExpiresAt
                    }
                };
            }

            // Parse the base64 encoded JSON data
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(entry.Data);
            if (cachedResponse == null) return null;

            var detailedEntry = new DetailedCacheEntry
            {
                CacheKeyHash = entry.Key,
                CacheKey = TryReconstructCacheKey(entry.Key),
                Request = new CachedRequestInfo(), // Initialize Request property
                Response = new CachedResponseInfo
                {
                    StatusCode = cachedResponse.StatusCode,
                    Headers = cachedResponse.Headers,
                    WasStreamed = cachedResponse.WasStreamed,
                    BodySizeBytes = cachedResponse.Body?.Length ?? 0
                },
                Metadata = new CacheMetadata
                {
                    CreatedAt = entry.CreatedAt,
                    ExpiresAt = entry.ExpiresAt
                }
            };

            // Decode response body
            if (cachedResponse.Body != null && cachedResponse.Body.Length > 0)
            {
                try
                {
                    var bodyText = System.Text.Encoding.UTF8.GetString(cachedResponse.Body);
                    detailedEntry.Response.Body = bodyText;
                    detailedEntry.Response.BodyPreview = bodyText.Length > 500 ? bodyText[..500] + "..." : bodyText;
                }
                catch
                {
                    detailedEntry.Response.Body = Convert.ToBase64String(cachedResponse.Body);
                    detailedEntry.Response.BodyPreview = $"[Binary data, {cachedResponse.Body.Length} bytes]";
                }
            }

            // Extract content type
            if (cachedResponse.Headers.TryGetValue("Content-Type", out var contentTypeValues))
            {
                detailedEntry.Response.ContentType = contentTypeValues.FirstOrDefault();
            }

            return detailedEntry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create detailed cache entry for key {Key}", entry.Key);
            return null;
        }
    }

    private string TryReconstructCacheKey(string hash)
    {
        // We can't fully reconstruct the original key since it's hashed
        // But we can provide information about what components likely made up the key
        return $"[Hashed key: {hash}] - Original key components not available (Method|Scheme|Host|Path|Query|Body)";
    }

    private void UpdateStatistics(CacheDumpStatistics stats, DetailedCacheEntry entry)
    {
        stats.TotalSizeBytes += entry.Response.BodySizeBytes;

        if (entry.Metadata.IsExpired)
        {
            stats.ExpiredEntries++;
        }

        // Update method statistics (if we had method info)
        var method = entry.Request?.Method ?? "Unknown";
        stats.Methods[method] = stats.Methods.GetValueOrDefault(method, 0) + 1;

        // Update content type statistics
        var contentType = entry.Response.ContentType ?? "Unknown";
        if (contentType.Contains(';'))
        {
            contentType = contentType.Split(';')[0].Trim();
        }
        stats.ContentTypes[contentType] = stats.ContentTypes.GetValueOrDefault(contentType, 0) + 1;

        // Update status code statistics
        stats.StatusCodes[entry.Response.StatusCode] = stats.StatusCodes.GetValueOrDefault(entry.Response.StatusCode, 0) + 1;
    }
}
