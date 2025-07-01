using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Utilities;
using Microsoft.Extensions.Options;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Default implementation of cache policy service
/// </summary>
public class CachePolicyService(
    IOptions<ProxySettings> settings,
    ILogger<CachePolicyService> logger) : ICachePolicyService
{
    private readonly ProxySettings _settings = settings.Value;

    public bool CanCache(HttpContext context)
    {
        // Check if caching is enabled at all
        if (!_settings.EnableMemoryCache && !_settings.EnableDiskCache)
        {
            logger.LogInformation("CanCache: Caching disabled - Memory: {MemoryEnabled}, Disk: {DiskEnabled}",
                _settings.EnableMemoryCache, _settings.EnableDiskCache);
            return false;
        }

        var request = context.Request;

        logger.LogInformation("CanCache: Checking request {Method} {Path}, Headers: {Headers}",
            request.Method, request.Path, string.Join(", ", request.Headers.Select(h => h.Key)));

        // Only cache GET and POST requests by default
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method))
        {
            logger.LogInformation("CanCache: Method {Method} not cacheable", request.Method);
            return false;
        }

        // Don't cache large requests
        const int maxCacheableBodySize = 1024 * 1024; // 1MB
        if (request.ContentLength.HasValue && request.ContentLength.Value > maxCacheableBodySize)
        {
            logger.LogInformation("CanCache: Request body too large for caching: {BodySize} bytes (max: {MaxSize} bytes)",
                request.ContentLength.Value, maxCacheableBodySize);
            return false;
        }

        // Don't cache file uploads
        if (!string.IsNullOrEmpty(request.ContentType) &&
            request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("CanCache: File upload not cacheable");
            return false;
        }

        if (request.Headers.ContainsKey("Cache-Control") &&
            request.Headers["Cache-Control"].ToString().Contains("no-cache"))
        {
            logger.LogInformation("CanCache: Request has Cache-Control: no-cache header");
            return false;
        }

        logger.LogInformation("CanCache: Request {Method} {Path} is cacheable", request.Method, request.Path);
        return true;
    }

    public TimeSpan GetCacheTtl(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        var method = request.Method;

        foreach (var rule in _settings.EndpointCacheRules)
        {
            if (!rule.Enabled)
                continue;

            if (!PatternMatcher.IsMatch(rule.PathPattern, path))
                continue;

            if (rule.Methods.Count > 0 && !rule.Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
                continue;

            logger.LogInformation("GetCacheTtl: Found matching rule for {Method} {Path} - TTL: {TtlSeconds}s, Pattern: {Pattern}",
                method, path, rule.TtlSeconds, rule.PathPattern);

            return TimeSpan.FromSeconds(rule.TtlSeconds);
        }

        return TimeSpan.Zero;
    }
}
