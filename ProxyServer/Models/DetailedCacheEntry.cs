using System.Text.Json.Serialization;

namespace DimonSmart.ProxyServer.Models;

/// <summary>
/// Detailed cache entry for human-readable dump operations
/// </summary>
public class DetailedCacheEntry
{
    [JsonPropertyName("cache_key")]
    public string CacheKey { get; set; } = string.Empty;

    [JsonPropertyName("cache_key_hash")]
    public string CacheKeyHash { get; set; } = string.Empty;

    [JsonPropertyName("request")]
    public CachedRequestInfo Request { get; set; } = new();

    [JsonPropertyName("response")]
    public CachedResponseInfo Response { get; set; } = new();

    [JsonPropertyName("metadata")]
    public CacheMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Information about the cached HTTP request
/// </summary>
public class CachedRequestInfo
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("query_parameters")]
    public Dictionary<string, string> QueryParameters { get; set; } = new();

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("body_preview")]
    public string? BodyPreview { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}

/// <summary>
/// Information about the cached HTTP response
/// </summary>
public class CachedResponseInfo
{
    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string[]> Headers { get; set; } = new();

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("body_preview")]
    public string? BodyPreview { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("was_streamed")]
    public bool WasStreamed { get; set; }

    [JsonPropertyName("body_size_bytes")]
    public int BodySizeBytes { get; set; }
}

/// <summary>
/// Cache metadata
/// </summary>
public class CacheMetadata
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("is_expired")]
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;

    [JsonPropertyName("ttl_remaining_seconds")]
    public double TtlRemainingSeconds => Math.Max(0, (ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
}

/// <summary>
/// Detailed cache dump container
/// </summary>
public class DetailedCacheDump
{
    [JsonPropertyName("dump_timestamp")]
    public DateTimeOffset DumpTimestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("total_entries")]
    public int TotalEntries { get; set; }

    [JsonPropertyName("filtered_entries")]
    public int FilteredEntries { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("entries")]
    public List<DetailedCacheEntry> Entries { get; set; } = new();

    [JsonPropertyName("statistics")]
    public CacheDumpStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Cache dump statistics
/// </summary>
public class CacheDumpStatistics
{
    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("expired_entries")]
    public int ExpiredEntries { get; set; }

    [JsonPropertyName("methods")]
    public Dictionary<string, int> Methods { get; set; } = new();

    [JsonPropertyName("content_types")]
    public Dictionary<string, int> ContentTypes { get; set; } = new();

    [JsonPropertyName("status_codes")]
    public Dictionary<int, int> StatusCodes { get; set; } = new();
}
