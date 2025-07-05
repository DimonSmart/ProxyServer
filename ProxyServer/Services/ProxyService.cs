using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Utilities;
using System.Diagnostics;
using System.Security.Authentication;

namespace DimonSmart.ProxyServer.Services;

public class ProxyService : IProxyService
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionDiagnosticsService _diagnosticsService;
    private readonly ILogger<ProxyService> _logger;
    private readonly ProxySettings _settings;

    public ProxyService(
        HttpClient httpClient, 
        IConnectionDiagnosticsService diagnosticsService,
        ILogger<ProxyService> logger,
        ProxySettings settings)
    {
        _httpClient = httpClient;
        _diagnosticsService = diagnosticsService;
        _logger = logger;
        _settings = settings;
    }

    public async Task<ProxyResponse> ForwardRequestAsync(HttpContext context, string targetUrl, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Log detailed request diagnostics
        if (_settings.Ssl.EnableSslDebugging)
        {
            _diagnosticsService.LogRequestDiagnostics(context, targetUrl);
            
            // Run connection diagnostics for first-time connections
            try
            {
                var diagnostics = await _diagnosticsService.DiagnoseConnectionAsync(targetUrl, cancellationToken);
                if (diagnostics.Error != null)
                {
                    _logger.LogWarning("Connection diagnostics detected issues: {Error}", diagnostics.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Connection diagnostics failed, continuing with request");
            }
        }

        var requestMessage = CreateRequestMessage(context, targetUrl);

        try
        {
            using var upstreamResponse = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            stopwatch.Stop();

            // Log response diagnostics
            if (_settings.Ssl.EnableSslDebugging)
            {
                _diagnosticsService.LogResponseDiagnostics(upstreamResponse, stopwatch.Elapsed);
            }

            // Determine if we should stream based on response characteristics
            var shouldStream = ShouldUseStreaming(upstreamResponse, context.Request);

            if (shouldStream)
            {
                return await HandleStreamingResponse(context, upstreamResponse, cancellationToken);
            }
            else
            {
                return await HandleBufferedResponse(upstreamResponse, cancellationToken);
            }
        }
        catch (AuthenticationException ex) when (ex.Message.Contains("frame size") || ex.Message.Contains("corrupted frame"))
        {
            _diagnosticsService.LogSslException(ex, targetUrl);
            
            // Additional analysis for common issues
            var targetUri = new Uri(targetUrl);
            var requestScheme = context.Request.Scheme;
            
            if (requestScheme == "https" && targetUri.Scheme == "http")
            {
                throw new InvalidOperationException(
                    $"Protocol mismatch detected: Client sent HTTPS request but upstream server '{targetUrl}' uses HTTP. " +
                    $"Either configure the client to use HTTP or set up SSL termination on the upstream server.", ex);
            }
            
            throw new InvalidOperationException($"SSL/TLS handshake failed with upstream server. " +
                $"This usually indicates a protocol mismatch (HTTPS client -> HTTP upstream) or certificate issues. " +
                $"Check logs for detailed diagnostics. Original error: {ex.Message}", ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException authEx)
        {
            _diagnosticsService.LogSslException(authEx, targetUrl);
            throw new InvalidOperationException($"HTTPS connection authentication failed: {authEx.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogError("Request timeout after {ElapsedMs}ms to {TargetUrl}", stopwatch.ElapsedMilliseconds, targetUrl);
            throw new HttpRequestException($"Upstream request timed out after {stopwatch.ElapsedMilliseconds}ms", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Proxy request failed after {ElapsedMs}ms to {TargetUrl}", stopwatch.ElapsedMilliseconds, targetUrl);
            throw new HttpRequestException($"Proxy request failed: {ex.Message}", ex);
        }
    }

    private async Task<ProxyResponse> HandleStreamingResponse(HttpContext context, HttpResponseMessage upstreamResponse, CancellationToken cancellationToken)
    {
        var headers = CollectResponseHeaders(upstreamResponse);

        // Set response status and headers immediately for streaming
        context.Response.StatusCode = (int)upstreamResponse.StatusCode;
        CopyResponseHeaders(context, headers);

        var bodyBuffer = new List<byte>();
        var buffer = new byte[8192];

        using var contentStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            // Check for cancellation before writing
            cancellationToken.ThrowIfCancellationRequested();

            // Stream to client immediately for real-time response
            await context.Response.Body.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            // Collect data for caching purposes
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, 0, chunk, 0, bytesRead);
            bodyBuffer.AddRange(chunk);
        }

        return new ProxyResponse((int)upstreamResponse.StatusCode, headers, bodyBuffer.ToArray(), true);
    }

    private async Task<ProxyResponse> HandleBufferedResponse(HttpResponseMessage upstreamResponse, CancellationToken cancellationToken)
    {
        var headers = CollectResponseHeaders(upstreamResponse);
        var body = await upstreamResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        return new ProxyResponse((int)upstreamResponse.StatusCode, headers, body, false);
    }

    private static bool ShouldUseStreaming(HttpResponseMessage upstreamResponse, HttpRequest originalRequest)
    {
        // Check for explicit streaming content types
        var contentType = upstreamResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        var isStreamingContentType = contentType != null && (
            contentType.Contains("text/event-stream") ||
            contentType.Contains("application/x-ndjson") ||
            contentType.Contains("application/stream+json"));

        // Check if response has chunked transfer encoding
        var isChunked = upstreamResponse.Headers.TransferEncodingChunked == true;

        // Check original request for streaming indicators
        var requestAccept = originalRequest.Headers["Accept"].ToString().ToLowerInvariant();
        var clientExpectsStreaming = requestAccept.Contains("text/event-stream") ||
                                   requestAccept.Contains("application/stream+json");

        // Only use streaming for explicit streaming responses
        return isStreamingContentType || isChunked || clientExpectsStreaming;
    }

    private static HttpRequestMessage CreateRequestMessage(HttpContext context, string targetUrl)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(targetUrl)
        };

        CopyRequestHeaders(context, requestMessage);
        CopyRequestBody(context, requestMessage);

        return requestMessage;
    }

    private static void CopyRequestHeaders(HttpContext context, HttpRequestMessage requestMessage)
    {
        foreach (var header in context.Request.Headers)
        {
            // Skip Host header as it will be set by HttpClient automatically
            if (!string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }

    private static void CopyRequestBody(HttpContext context, HttpRequestMessage requestMessage)
    {
        if (ShouldCopyBody(context.Request.Method))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);

            // Copy Content-Type header if present
            var contentType = context.Request.Headers["Content-Type"].ToString();
            if (!string.IsNullOrEmpty(contentType))
            {
                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            // Copy Content-Length header if present
            var contentLength = context.Request.Headers["Content-Length"].ToString();
            if (!string.IsNullOrEmpty(contentLength) && long.TryParse(contentLength, out var length))
            {
                requestMessage.Content.Headers.ContentLength = length;
            }
        }
    }

    private static bool ShouldCopyBody(string method)
    {
        return !HttpMethods.IsGet(method) &&
               !HttpMethods.IsHead(method) &&
               !HttpMethods.IsDelete(method) &&
               !HttpMethods.IsTrace(method);
    }

    private static Dictionary<string, string[]> CollectResponseHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string[]>();

        // Copy response headers
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        // Copy content headers
        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        return headers;
    }

    private static void CopyResponseHeaders(HttpContext context, Dictionary<string, string[]> headers)
    {
        foreach (var header in headers)
        {
            // Skip headers that ASP.NET Core manages automatically or that can't be set during streaming
            if (!HttpHeaderUtilities.IsRestrictedHeader(header.Key))
            {
                try
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
                catch
                {
                    // Ignore headers that can't be set
                }
            }
        }

        // For streaming responses, remove problematic headers
        context.Response.Headers.Remove("content-length");
        context.Response.Headers.Remove("transfer-encoding");
    }
}
