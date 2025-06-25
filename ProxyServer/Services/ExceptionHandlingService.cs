using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Service for handling exceptions in the proxy server
/// </summary>
public class ExceptionHandlingService(ILogger<ExceptionHandlingService> logger) : IExceptionHandlingService
{
    private readonly ILogger<ExceptionHandlingService> _logger = logger;

    public (int statusCode, string message) GetErrorResponse(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => (StatusCodes.Status502BadGateway, $"Bad Gateway: {httpEx.Message}"),
            TaskCanceledException when exception.InnerException is TimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Gateway Timeout: The upstream server took too long to respond"),
            TaskCanceledException =>
                (StatusCodes.Status408RequestTimeout, "Request Timeout: The request was cancelled"),
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized: Access denied"),
            ArgumentException argEx =>
                (StatusCodes.Status400BadRequest, $"Bad Request: {argEx.Message}"),
            NotSupportedException =>
                (StatusCodes.Status405MethodNotAllowed, "Method Not Allowed: The requested method is not supported"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error: An unexpected error occurred")
        };
    }

    public void LogException(Exception exception, string context = "")
    {
        var logMessage = string.IsNullOrEmpty(context)
            ? "An unhandled exception occurred"
            : $"An unhandled exception occurred in {context}";

        switch (exception)
        {
            case HttpRequestException:
                _logger.LogWarning(exception, "{Message} - Upstream server error", logMessage);
                break;
            case TaskCanceledException:
                _logger.LogWarning(exception, "{Message} - Request timeout or cancellation", logMessage);
                break;
            case UnauthorizedAccessException:
                _logger.LogWarning(exception, "{Message} - Access denied", logMessage);
                break;
            case ArgumentException:
                _logger.LogWarning(exception, "{Message} - Invalid request parameters", logMessage);
                break;
            default:
                _logger.LogError(exception, logMessage);
                break;
        }
    }
}
