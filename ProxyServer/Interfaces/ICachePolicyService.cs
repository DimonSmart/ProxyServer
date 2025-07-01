namespace DimonSmart.ProxyServer.Interfaces;

public interface ICachePolicyService
{
    bool CanCache(HttpContext context);

    TimeSpan GetCacheTtl(HttpContext context);
}
