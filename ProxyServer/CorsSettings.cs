namespace DimonSmart.ProxyServer;

public class CorsSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// List of allowed origins. Empty list means allow all origins.
    /// Example: ["http://localhost:5042", "https://mydomain.com"]
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers) in CORS requests.
    /// Default is true for authentication support.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// Maximum age in seconds for preflight request caching. Default is 1 hour.
    /// </summary>
    public int MaxAgeSeconds { get; set; } = 3600;

    /// <summary>
    /// Additional headers to expose to the client beyond the default set.
    /// </summary>
    public List<string> AdditionalExposedHeaders { get; set; } = new();

    /// <summary>
    /// Additional headers to allow in requests beyond the default set.
    /// </summary>
    public List<string> AdditionalAllowedHeaders { get; set; } = new();
}
