using Microsoft.Net.Http.Headers;

namespace DimonSmart.ProxyServer.Middleware;

public class CorsMiddleware(RequestDelegate next, ILogger<CorsMiddleware> logger, ProxySettings settings)
{
    private readonly ProxySettings _settings = settings;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Cors.Enabled)
        {
            await next(context);
            return;
        }

        AddCorsHeaders(context);

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await HandlePreflightRequest(context);
            return;
        }

        await next(context);
    }

    private void AddCorsHeaders(HttpContext context)
    {
        var response = context.Response;
        var request = context.Request;

        var origin = request.Headers[HeaderNames.Origin].FirstOrDefault();

        var allowedOrigin = GetAllowedOrigin(origin);
        if (!string.IsNullOrEmpty(allowedOrigin))
        {
            response.Headers[HeaderNames.AccessControlAllowOrigin] = allowedOrigin;
        }

        if (_settings.Cors.AllowCredentials)
        {
            response.Headers[HeaderNames.AccessControlAllowCredentials] = "true";
        }

        // Allow common HTTP methods
        response.Headers[HeaderNames.AccessControlAllowMethods] = $"{HttpMethods.Get}, {HttpMethods.Post}, {HttpMethods.Put}, {HttpMethods.Delete}, {HttpMethods.Options}, {HttpMethods.Patch}, {HttpMethods.Head}";

        // Build allowed headers list
        var allowedHeaders = new List<string>
        {
            HeaderNames.Origin, "X-Requested-With", HeaderNames.ContentType, HeaderNames.Accept,
            HeaderNames.Authorization, HeaderNames.CacheControl, "Pragma", "X-Custom-Header"
        };
        allowedHeaders.AddRange(_settings.Cors.AdditionalAllowedHeaders);

        response.Headers[HeaderNames.AccessControlAllowHeaders] = string.Join(", ", allowedHeaders);

        // Set max age for preflight cache
        response.Headers[HeaderNames.AccessControlMaxAge] = _settings.Cors.MaxAgeSeconds.ToString();

        // Build exposed headers list
        var exposedHeaders = new List<string>
        {
            HeaderNames.ContentLength, HeaderNames.ContentType, HeaderNames.Date, HeaderNames.Server, "X-Cache-Status"
        };
        exposedHeaders.AddRange(_settings.Cors.AdditionalExposedHeaders);

        response.Headers[HeaderNames.AccessControlExposeHeaders] = string.Join(", ", exposedHeaders);

        logger.LogDebug("CORS headers added for origin: {Origin}", origin ?? "none");
    }

    private string? GetAllowedOrigin(string? requestOrigin)
    {
        // If no specific origins configured, allow any origin
        if (_settings.Cors.AllowedOrigins.Count == 0)
        {
            return requestOrigin ?? "*";
        }

        // If request has no origin, allow it (non-browser requests)
        if (string.IsNullOrEmpty(requestOrigin))
        {
            return "*";
        }

        // Check if the request origin is in the allowed list
        if (_settings.Cors.AllowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return requestOrigin;
        }

        // Check for wildcard patterns (simple implementation)
        foreach (var allowedOrigin in _settings.Cors.AllowedOrigins)
        {
            if (allowedOrigin == "*" || IsOriginMatchingPattern(requestOrigin, allowedOrigin))
            {
                return requestOrigin;
            }
        }

        // Origin not allowed
        logger.LogWarning("CORS request from unauthorized origin: {Origin}", requestOrigin);
        return null;
    }

    private static bool IsOriginMatchingPattern(string origin, string pattern)
    {
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private async Task HandlePreflightRequest(HttpContext context)
    {
        logger.LogInformation("CORS preflight request from origin: {Origin}",
            context.Request.Headers[HeaderNames.Origin].FirstOrDefault() ?? "none");

        context.Response.StatusCode = 200;

        context.Response.ContentLength = 0;

        await context.Response.CompleteAsync();
    }
}
