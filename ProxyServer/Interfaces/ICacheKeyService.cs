namespace DimonSmart.ProxyServer.Interfaces;

public interface ICacheKeyService
{
    Task<string> GenerateCacheKeyAsync(HttpContext context);
}
