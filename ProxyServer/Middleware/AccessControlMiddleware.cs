using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Middleware;

public class AccessControlMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAccessControlService _accessControlService;
    private readonly ILogger<AccessControlMiddleware> _logger;

    public AccessControlMiddleware(RequestDelegate next, IAccessControlService accessControlService, ILogger<AccessControlMiddleware> logger)
    {
        _next = next;
        _accessControlService = accessControlService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var requestPath = context.Request.Path;
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        // Allow health check endpoints to bypass authentication
        if (IsHealthCheckEndpoint(context.Request.Path))
        {
            _logger.LogDebug("Health check request from {RemoteIP} to {Path} - bypassing authentication", remoteIp, requestPath);
            await _next(context);
            return;
        }

        var (isAllowed, statusCode, errorMessage) = _accessControlService.Validate(context);

        if (!isAllowed)
        {
            _logger.LogWarning("Access blocked: IP={RemoteIP}, Path={Path}, UserAgent={UserAgent}, Status={StatusCode}, Reason={ErrorMessage}",
                remoteIp, requestPath, userAgent, statusCode, errorMessage);

            if (statusCode == StatusCodes.Status401Unauthorized)
            {
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ProxyServer\"";
            }

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(errorMessage ?? "Access denied");
            return;
        }

        _logger.LogDebug("Access granted for IP {RemoteIP} to {Path}", remoteIp, requestPath);
        await _next(context);
    }

    /// <summary>
    /// Determines if the requested path is a health check endpoint that should bypass authentication
    /// </summary>
    /// <param name="path">The request path</param>
    /// <returns>True if the path is a health check endpoint</returns>
    private static bool IsHealthCheckEndpoint(PathString path)
    {
        return path.StartsWithSegments("/health") ||
               path.StartsWithSegments("/ping") ||
               path.StartsWithSegments("/stats");
    }
}
