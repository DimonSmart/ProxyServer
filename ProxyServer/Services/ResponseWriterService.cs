using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Service for writing cached responses to HTTP context
/// </summary>
public class ResponseWriterService : IResponseWriterService
{
    private readonly ProxySettings _settings;
    private readonly ILogger<ResponseWriterService> _logger;

    public ResponseWriterService(ProxySettings settings, ILogger<ResponseWriterService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task WriteCachedResponseAsync(HttpContext context, CachedResponse cachedResponse, CancellationToken cancellationToken = default)
    {
        var response = context.Response;
        response.StatusCode = cachedResponse.StatusCode;

        // Headers that should not be copied to avoid conflicts
        var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Transfer-Encoding",
            "Content-Length",
            "Connection",
            "Date",
            "Server"
        };

        // Set headers, excluding problematic ones
        foreach (var header in cachedResponse.Headers)
        {
            if (!skipHeaders.Contains(header.Key) && !response.Headers.ContainsKey(header.Key))
            {
                try
                {
                    response.Headers[header.Key] = header.Value;
                }
                catch
                {
                    // Ignore headers that can't be set
                }
            }
        }

        // Extract content type from headers if present
        if (cachedResponse.Headers.TryGetValue("Content-Type", out var contentTypeValues) && contentTypeValues.Length > 0)
        {
            response.ContentType = contentTypeValues[0];
        }

        // Always set content length for cached responses to avoid chunked encoding conflicts
        if (cachedResponse.Body?.Length > 0)
        {
            response.ContentLength = cachedResponse.Body.Length;
        }
        else
        {
            response.ContentLength = 0;
        }

        // Write the body with streaming support if enabled and response was originally streamed
        if (cachedResponse.Body?.Length > 0)
        {
            if (_settings.StreamingCache.EnableStreamingCache && cachedResponse.WasStreamed)
            {
                // Stream the response body in chunks for better performance
                const int bufferSize = 8192;
                var buffer = new byte[bufferSize];
                var offset = 0;

                while (offset < cachedResponse.Body.Length)
                {
                    var bytesToRead = Math.Min(bufferSize, cachedResponse.Body.Length - offset);
                    Array.Copy(cachedResponse.Body, offset, buffer, 0, bytesToRead);
                    await response.Body.WriteAsync(buffer, 0, bytesToRead, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    offset += bytesToRead;
                }
            }
            else
            {
                // Write the entire response at once
                await response.Body.WriteAsync(cachedResponse.Body, 0, cachedResponse.Body.Length, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
