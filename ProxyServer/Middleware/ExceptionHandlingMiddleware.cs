using DimonSmart.ProxyServer.Interfaces;
using System.Text.Json;

namespace DimonSmart.ProxyServer.Middleware;

/// <summary>
/// Middleware for global exception handling in the proxy server
/// </summary>
public class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly IExceptionHandlingService _exceptionHandlingService;

    public ExceptionHandlingMiddleware(IExceptionHandlingService exceptionHandlingService)
    {
        _exceptionHandlingService = exceptionHandlingService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _exceptionHandlingService.LogException(exception, "response already started");
            return;
        }

        _exceptionHandlingService.LogException(exception, $"{context.Request.Method} {context.Request.Path}");

        var (statusCode, message) = _exceptionHandlingService.GetErrorResponse(exception);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = new
            {
                message,
                type = exception.GetType().Name,
                timestamp = DateTime.UtcNow
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
