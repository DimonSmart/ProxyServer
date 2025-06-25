namespace DimonSmart.ProxyServer.Interfaces;

public interface IProxyService
{
    Task<ProxyResponse> ForwardRequestAsync(HttpContext context, string targetUrl, CancellationToken cancellationToken = default);
}

public record ProxyResponse(int StatusCode, Dictionary<string, string[]> Headers, byte[] Body);
