namespace DimonSmart.ProxyServer;

public class ProxySettings
{
    public List<CredentialPair> AllowedCredentials { get; set; } = new();
    public string UpstreamUrl { get; set; } = "http://localhost:11411";
    public bool UseMemoryCache { get; set; }
    public int CacheDurationSeconds { get; set; } = 60;
    public int CacheMaxEntries { get; set; } = 1000;
}