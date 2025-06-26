using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;
using DimonSmart.ProxyServer.Utilities;

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

        // Set headers, excluding restricted ones
        foreach (var header in cachedResponse.Headers)
        {
            if (!HttpHeaderUtilities.IsRestrictedHeader(header.Key) && !response.Headers.ContainsKey(header.Key))
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

        // Write the body with streaming support if enabled and response was originally streamed
        if (cachedResponse.Body?.Length > 0)
        {
            if (_settings.StreamingCache.EnableStreamingCache && cachedResponse.WasStreamed)
            {
                await StreamResponseInChunks(context, cachedResponse.Body, cancellationToken);
            }
            else
            {
                // Write the entire response at once
                await response.Body.WriteAsync(cachedResponse.Body, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Streams response body in chunks with configurable delays
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="body">Response body to stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StreamResponseInChunks(HttpContext context, byte[] body, CancellationToken cancellationToken)
    {
        var response = context.Response;
        var chunkSize = _settings.StreamingCache.ChunkSize;
        var delayMs = _settings.StreamingCache.ChunkDelayMs;
        var totalLength = body.Length;
        var offset = 0;

        // Remove problematic headers for streaming
        response.Headers.Remove("content-length");
        response.Headers.Remove("transfer-encoding");

        while (offset < totalLength && !cancellationToken.IsCancellationRequested)
        {
            var remainingBytes = totalLength - offset;
            var currentChunkSize = Math.Min(chunkSize, remainingBytes);

            await response.Body.WriteAsync(body, offset, currentChunkSize, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);

            offset += currentChunkSize;

            // Add delay between chunks if configured and not the last chunk
            if (delayMs > 0 && offset < totalLength)
            {
                try
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // If cancelled during delay, break the loop
                    break;
                }
            }
        }
    }
}
