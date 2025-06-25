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
}
