using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Middleware;

public class ProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyService _proxyService;
    private readonly ICacheService _cacheService;
    private readonly ProxySettings _settings;

    public ProxyMiddleware(
        RequestDelegate next,
        IProxyService proxyService,
        ICacheService cacheService,
        ProxySettings settings)
    {
        _next = next;
        _proxyService = proxyService;
        _cacheService = cacheService;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var targetUrl = _settings.UpstreamUrl + context.Request.Path + context.Request.QueryString;

        if (_cacheService.CanCache(context))
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
        var cacheKey = await _cacheService.GenerateCacheKeyAsync(context);
        var cachedResponse = await _cacheService.GetAsync<CachedResponse>(cacheKey);

        if (cachedResponse != null)
        {
            await WriteResponse(context, cachedResponse.StatusCode, cachedResponse.Headers, cachedResponse.Body);
            return;
        }

        var response = await _proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);

        // Cache the complete response after processing
        if (ShouldCacheResponse(response))
        {
            var cacheExpiration = TimeSpan.FromSeconds(_settings.CacheDurationSeconds);
            var cachedResponseToStore = new CachedResponse(response.StatusCode, response.Headers, response.Body);
            await _cacheService.SetAsync(cacheKey, cachedResponseToStore, cacheExpiration);
        }
    }

    private async Task HandleDirectRequest(HttpContext context, string targetUrl)
    {
        await _proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);
    }

    private static async Task WriteResponse(HttpContext context, int statusCode, Dictionary<string, string[]> headers, byte[] body)
    {
        context.Response.StatusCode = statusCode;

        foreach (var header in headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        context.Response.Headers.Remove("transfer-encoding");
        context.Response.Headers.Remove("content-encoding");

        await context.Response.Body.WriteAsync(body);
    }

    private static bool ShouldCacheResponse(ProxyResponse response)
    {
        // Only cache successful responses (2xx status codes)
        return response.StatusCode >= 200 && response.StatusCode < 300;
    }
}
