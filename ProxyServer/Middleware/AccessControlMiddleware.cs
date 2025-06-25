using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Middleware;

public class AccessControlMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAccessControlService _accessControlService;

    public AccessControlMiddleware(RequestDelegate next, IAccessControlService accessControlService)
    {
        _next = next;
        _accessControlService = accessControlService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow health check endpoints to bypass authentication
        if (IsHealthCheckEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var (isAllowed, statusCode, errorMessage) = _accessControlService.Validate(context);

        if (!isAllowed)
        {
            if (statusCode == StatusCodes.Status401Unauthorized)
            {
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ProxyServer\"";
            }

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(errorMessage ?? "Access denied");
            return;
        }

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
