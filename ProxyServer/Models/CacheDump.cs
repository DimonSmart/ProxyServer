using System.Text.Json.Serialization;

namespace DimonSmart.ProxyServer.Models;

/// <summary>
/// Container for cache dump with metadata
/// </summary>
public class CacheDump
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
    public List<CacheEntry> Entries { get; set; } = new();
}
