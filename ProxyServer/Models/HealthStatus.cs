namespace DimonSmart.ProxyServer.Models;

/// <summary>
/// Represents the health status of the proxy server
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Server version information
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Server uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Upstream server configuration
    /// </summary>
    public string UpstreamUrl { get; set; } = string.Empty;

    /// <summary>
    /// Current timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional server information
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}
