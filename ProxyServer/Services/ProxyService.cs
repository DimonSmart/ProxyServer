using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

public class ProxyService : IProxyService
{
    private readonly HttpClient _httpClient;

    public ProxyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProxyResponse> ForwardRequestAsync(HttpContext context, string targetUrl, CancellationToken cancellationToken = default)
    {
        var requestMessage = CreateRequestMessage(context, targetUrl);

        try
        {
            using var upstreamResponse = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            var headers = CollectResponseHeaders(upstreamResponse);
            var body = await upstreamResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            return new ProxyResponse((int)upstreamResponse.StatusCode, headers, body);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Proxy request failed: {ex.Message}", ex);
        }
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
            var contentType = context.Request.Headers["Content-Type"].ToString();
            if (!string.IsNullOrEmpty(contentType))
            {
                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
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

        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        return headers;
    }
}
