using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Middleware;

public class ProxyMiddleware(
    RequestDelegate next,
    IProxyService proxyService,
    ICacheService cacheService,
    ICacheKeyService cacheKeyService,
    IResponseWriterService responseWriterService,
    ICachePolicyService cachePolicyService,
    ProxySettings settings,
    ILogger<ProxyMiddleware> logger)
{
    private readonly ProxySettings _settings = settings;
    private readonly ILogger<ProxyMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var targetUrl = _settings.UpstreamUrl + context.Request.Path + context.Request.QueryString;

        _logger.LogInformation("ProxyMiddleware: Processing request {Method} {Path}, CanCache: {CanCache}",
            context.Request.Method, context.Request.Path, cachePolicyService.CanCache(context));

        if (cachePolicyService.CanCache(context))
        {
            await HandleCachedRequest(context, targetUrl);
        }
        else
        {
            await HandleDirectRequest(context, targetUrl);
        }
    }

    private async Task HandleCachedRequest(HttpContext context, string targetUrl)
    {
        var cacheKey = await cacheKeyService.GenerateCacheKeyAsync(context);
        _logger.LogInformation("ProxyMiddleware: Generated cache key: {CacheKey}", cacheKey);

        var cachedResponse = await cacheService.GetAsync<CachedResponse>(cacheKey);

        if (cachedResponse != null)
        {
            _logger.LogInformation("ProxyMiddleware: Cache HIT for key: {CacheKey}", cacheKey);

            await responseWriterService.WriteCachedResponseAsync(context, cachedResponse, context.RequestAborted);
            return;
        }

        _logger.LogInformation("ProxyMiddleware: Cache MISS for key: {CacheKey}, forwarding to upstream", cacheKey);

        var response = await proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);

        // For streamed responses, the response has already been sent to the client
        // so we only need to cache it if caching conditions are met
        if (response.WasStreamed)
        {
            // Response was already sent to client during streaming, just cache it
            if (ShouldCacheResponse(response))
            {
                _logger.LogDebug("ProxyMiddleware: Caching streamed response for key: {CacheKey}", cacheKey);
                var cacheExpiration = GetCacheExpiration();
                var cachedResponseToStore = new CachedResponse(response.StatusCode, response.Headers, response.Body ?? [], response.WasStreamed);
                await cacheService.SetAsync(cacheKey, cachedResponseToStore, cacheExpiration);
            }
            else
            {
                _logger.LogDebug("ProxyMiddleware: Not caching streamed response for key {CacheKey} - status: {StatusCode}", cacheKey, response.StatusCode);
            }
        }
        else
        {
            // For non-streamed responses, we need to send the response to the client
            context.Response.StatusCode = response.StatusCode;

            // Headers that should not be copied to avoid conflicts
            var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Transfer-Encoding",
                "Content-Length",
                "Connection",
                "Date",
                "Server"
            };

            // Copy headers, excluding problematic ones
            foreach (var header in response.Headers)
            {
                if (!skipHeaders.Contains(header.Key))
                {
                    try
                    {
                        context.Response.Headers[header.Key] = header.Value;
                    }
                    catch
                    {
                        // Ignore headers that can't be set
                    }
                }
            }

            // Always set content length to avoid chunked encoding conflicts
            if (response.Body?.Length > 0)
            {
                context.Response.ContentLength = response.Body.Length;
                await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            }
            else
            {
                context.Response.ContentLength = 0;
            }

            await context.Response.Body.FlushAsync(context.RequestAborted);

            // Cache the response if conditions are met
            if (ShouldCacheResponse(response))
            {
                _logger.LogDebug("ProxyMiddleware: Caching response for key: {CacheKey}", cacheKey);
                var cacheExpiration = GetCacheExpiration();
                var cachedResponseToStore = new CachedResponse(response.StatusCode, response.Headers, response.Body ?? [], response.WasStreamed);
                await cacheService.SetAsync(cacheKey, cachedResponseToStore, cacheExpiration);
            }
            else
            {
                _logger.LogDebug("ProxyMiddleware: Not caching response for key {CacheKey} - status: {StatusCode}", cacheKey, response.StatusCode);
            }
        }
    }

    private async Task HandleDirectRequest(HttpContext context, string targetUrl)
    {
        var response = await proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);

        // For streamed responses, the response has already been sent to the client
        if (!response.WasStreamed)
        {
            // For non-streamed responses, we need to send the response to the client
            context.Response.StatusCode = response.StatusCode;

            // Headers that should not be copied to avoid conflicts
            var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Transfer-Encoding",
                "Content-Length",
                "Connection",
                "Date",
                "Server"
            };

            // Copy headers, excluding problematic ones
            foreach (var header in response.Headers)
            {
                if (!skipHeaders.Contains(header.Key))
                {
                    try
                    {
                        context.Response.Headers[header.Key] = header.Value;
                    }
                    catch
                    {
                        // Ignore headers that can't be set
                    }
                }
            }

            // Always set content length to avoid chunked encoding conflicts
            if (response.Body?.Length > 0)
            {
                context.Response.ContentLength = response.Body.Length;
                await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            }
            else
            {
                context.Response.ContentLength = 0;
            }

            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }

    private static bool ShouldCacheResponse(ProxyResponse response)
    {
        // Only cache successful responses (2xx status codes)
        return response.StatusCode >= 200 && response.StatusCode < 300;
    }

    private TimeSpan GetCacheExpiration()
    {
        // If both memory and disk cache are enabled, use the longer TTL (disk cache)
        // so that data can survive in disk even after memory cache expires
        if (_settings.EnableMemoryCache && _settings.EnableDiskCache)
        {
            return TimeSpan.FromSeconds(_settings.DiskCache.TtlSeconds);
        }

        // If only disk cache is enabled, use disk TTL
        if (_settings.EnableDiskCache)
        {
            return TimeSpan.FromSeconds(_settings.DiskCache.TtlSeconds);
        }

        // If only memory cache is enabled, use memory TTL
        if (_settings.EnableMemoryCache)
        {
            return TimeSpan.FromSeconds(_settings.MemoryCache.TtlSeconds);
        }

        // Fallback - should not happen in normal scenarios
        return TimeSpan.FromHours(1);
    }
}
