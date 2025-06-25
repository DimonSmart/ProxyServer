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
            // Always use WriteCachedResponseAsync to preserve original behavior and headers
            // Disable streaming cache simulation to fix test issues
            await _cacheService.WriteCachedResponseAsync(context, cachedResponse, context.RequestAborted);
            return;
        }

        var response = await _proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);

        // For streamed responses, the response has already been sent to the client
        // so we only need to cache it if caching conditions are met
        if (response.WasStreamed)
        {
            // Response was already sent to client during streaming, just cache it
            if (ShouldCacheResponse(response))
            {
                var cacheExpiration = TimeSpan.FromSeconds(_settings.CacheDurationSeconds);
                var cachedResponseToStore = new CachedResponse(response.StatusCode, response.Headers, response.Body, response.WasStreamed);
                await _cacheService.SetAsync(cacheKey, cachedResponseToStore, cacheExpiration);
            }
        }
        else
        {
            // For non-streamed responses, we need to send the response to the client
            context.Response.StatusCode = response.StatusCode;

            // Copy headers
            foreach (var header in response.Headers)
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

            // Write response body
            await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);

            // Cache the response if conditions are met
            if (ShouldCacheResponse(response))
            {
                var cacheExpiration = TimeSpan.FromSeconds(_settings.CacheDurationSeconds);
                var cachedResponseToStore = new CachedResponse(response.StatusCode, response.Headers, response.Body, response.WasStreamed);
                await _cacheService.SetAsync(cacheKey, cachedResponseToStore, cacheExpiration);
            }
        }
    }

    private async Task HandleDirectRequest(HttpContext context, string targetUrl)
    {
        var response = await _proxyService.ForwardRequestAsync(context, targetUrl, context.RequestAborted);

        // For streamed responses, the response has already been sent to the client
        if (!response.WasStreamed)
        {
            // For non-streamed responses, we need to send the response to the client
            context.Response.StatusCode = response.StatusCode;

            // Copy headers
            foreach (var header in response.Headers)
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

            // Write response body
            await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }

    private static bool ShouldCacheResponse(ProxyResponse response)
    {
        // Only cache successful responses (2xx status codes)
        return response.StatusCode >= 200 && response.StatusCode < 300;
    }
}
