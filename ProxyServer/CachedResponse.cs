namespace DimonSmart.ProxyServer;

public record CachedResponse(int StatusCode, Dictionary<string, string[]> Headers, byte[] Body);