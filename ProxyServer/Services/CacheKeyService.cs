using DimonSmart.ProxyServer.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Service for generating cache keys from HTTP context
/// </summary>
public class CacheKeyService : ICacheKeyService
{
    private readonly ILogger<CacheKeyService> _logger;

    public CacheKeyService(ILogger<CacheKeyService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateCacheKeyAsync(HttpContext context)
    {
        var request = context.Request;
        var keyBuilder = new StringBuilder();

        keyBuilder.Append($"{request.Method}|{request.Scheme}|{request.Host}|{request.Path}");

        if (request.Query.Count > 0)
        {
            var sortedQuery = request.Query.OrderBy(kv => kv.Key);
            keyBuilder.Append("|");
            keyBuilder.Append(string.Join("&", sortedQuery.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        request.EnableBuffering();
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Position = 0;

        keyBuilder.Append($"|{body}");
        _logger.LogDebug("Generated cache key including request body ({BodySize} bytes)", body?.Length ?? 0);

        var key = keyBuilder.ToString();
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }
}
