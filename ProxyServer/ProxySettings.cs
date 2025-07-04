using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer;

public class ProxySettings
{
    public List<CredentialPair> AllowedCredentials { get; set; } = new();
    public string UpstreamUrl { get; set; } = string.Empty;
    public bool EnableMemoryCache { get; set; } = true;
    public bool EnableDiskCache { get; set; } = false;
    public int Port { get; set; } = 5000;
    public int? HttpsPort { get; set; }
    public bool ListenOnAllInterfaces { get; set; } = false;
    public int UpstreamTimeoutSeconds { get; set; } = 1800;

    public MemoryCacheSettings MemoryCache { get; set; } = new();
    public StreamingCacheSettings StreamingCache { get; set; } = new();
    public DiskCacheSettings DiskCache { get; set; } = new();
    public List<EndpointCacheRule> EndpointCacheRules { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
}
