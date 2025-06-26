using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Interfaces;

public interface IResponseWriterService
{
    Task WriteCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default);
}
