using Microsoft.Extensions.Logging;
using Xunit;

namespace ProxyServer.FunctionalTests;

/// <summary>
/// Tests for exception handling middleware functionality
/// </summary>
public class ExceptionHandlingTests
{
    [Fact]
    public void ExceptionHandlingService_Should_ReturnCorrectErrorForHttpRequestException()
    {
        // Arrange
        var logger = new TestLogger<DimonSmart.ProxyServer.Services.ExceptionHandlingService>();
        var service = new DimonSmart.ProxyServer.Services.ExceptionHandlingService(logger);
        var exception = new HttpRequestException("Connection failed");

        // Act
        (var statusCode, var message) = service.GetErrorResponse(exception);

        // Assert
        Assert.Equal(502, statusCode);
        Assert.Contains("Bad Gateway", message);
        Assert.Contains("Connection failed", message);
    }

    [Fact]
    public void ExceptionHandlingService_Should_ReturnCorrectErrorForTimeoutException()
    {
        // Arrange
        var logger = new TestLogger<DimonSmart.ProxyServer.Services.ExceptionHandlingService>();
        var service = new DimonSmart.ProxyServer.Services.ExceptionHandlingService(logger);
        var timeoutException = new TimeoutException("Request timeout");
        var taskCancelledException = new TaskCanceledException("Task was cancelled", timeoutException);

        // Act
        (var statusCode, var message) = service.GetErrorResponse(taskCancelledException);

        // Assert
        Assert.Equal(504, statusCode);
        Assert.Contains("Gateway Timeout", message);
    }

    [Fact]
    public void ExceptionHandlingService_Should_ReturnCorrectErrorForUnauthorizedException()
    {
        // Arrange
        var logger = new TestLogger<DimonSmart.ProxyServer.Services.ExceptionHandlingService>();
        var service = new DimonSmart.ProxyServer.Services.ExceptionHandlingService(logger);
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        (var statusCode, var message) = service.GetErrorResponse(exception);

        // Assert
        Assert.Equal(401, statusCode);
        Assert.Contains("Unauthorized", message);
    }

    [Fact]
    public void ExceptionHandlingService_Should_LogExceptionWithCorrectLevel()
    {
        // Arrange
        var logger = new TestLogger<DimonSmart.ProxyServer.Services.ExceptionHandlingService>();
        var service = new DimonSmart.ProxyServer.Services.ExceptionHandlingService(logger);
        var exception = new HttpRequestException("Connection failed");

        // Act
        service.LogException(exception, "test context");

        // Assert
        Assert.True(logger.HasLoggedWarning);
        Assert.Contains("test context", logger.LastLogMessage);
    }
}

/// <summary>
/// Test logger for capturing log messages in tests
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    public bool HasLoggedWarning { get; private set; }
    public bool HasLoggedError { get; private set; }
    public string LastLogMessage { get; private set; } = string.Empty;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LastLogMessage = formatter(state, exception);

        switch (logLevel)
        {
            case LogLevel.Warning:
                HasLoggedWarning = true;
                break;
            case LogLevel.Error:
                HasLoggedError = true;
                break;
        }
    }
}
