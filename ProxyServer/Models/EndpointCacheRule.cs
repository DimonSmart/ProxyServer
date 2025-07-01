namespace DimonSmart.ProxyServer.Models;

/// <summary>
/// Represents a cache rule for specific endpoint patterns
/// </summary>
public class EndpointCacheRule
{
    /// <summary>
    /// Pattern to match against request path. Supports wildcards (* and ?)
    /// Examples: "/api/tags", "/api/models/*", "/v1/*/list"
    /// </summary>
    public string PathPattern { get; set; } = string.Empty;

    /// <summary>
    /// HTTP methods this rule applies to. If empty, applies to all methods.
    /// Examples: ["GET"], ["GET", "POST"]
    /// </summary>
    public List<string> Methods { get; set; } = new();

    /// <summary>
    /// TTL in seconds for this endpoint
    /// </summary>
    public int TtlSeconds { get; set; }

    /// <summary>
    /// Optional description for this rule
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}
