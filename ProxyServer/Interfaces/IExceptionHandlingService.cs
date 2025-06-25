namespace DimonSmart.ProxyServer.Interfaces;

/// <summary>
/// Interface for exception handling service
/// </summary>
public interface IExceptionHandlingService
{
    /// <summary>
    /// Handles exceptions and determines appropriate HTTP response
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <returns>Tuple containing status code and error message</returns>
    (int statusCode, string message) GetErrorResponse(Exception exception);

    /// <summary>
    /// Logs the exception with appropriate level
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="context">Additional context information</param>
    void LogException(Exception exception, string context = "");
}
