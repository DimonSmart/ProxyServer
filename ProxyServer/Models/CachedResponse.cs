namespace DimonSmart.ProxyServer.Models;

public record CachedResponse(int StatusCode, Dictionary<string, string[]> Headers, byte[] Body, bool WasStreamed = false);