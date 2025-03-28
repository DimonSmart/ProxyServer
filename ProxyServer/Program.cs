using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using DimonSmart.ProxyServer;

var settingsFile = "settings.json";
var settings = File.Exists(settingsFile)
    ? JsonSerializer.Deserialize<ProxySettings>(File.ReadAllText(settingsFile))!
    : new ProxySettings();

// Configure services and memory cache if enabled.
var builder = WebApplication.CreateBuilder(args);
if (settings.UseMemoryCache)
{
    builder.Services.AddMemoryCache(options =>
    {
        if (settings.CacheMaxEntries > 0)
        {
            options.SizeLimit = settings.CacheMaxEntries;
        }
    });
}
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<AccessControlService>();

builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(5000, listenOptions =>
{
    // Используем созданный PFX-файл и пароль
    listenOptions.UseHttps("dimon2.pfx", "nomid");
}));
var app = builder.Build();

// Access control middleware using the AccessControlService.
app.Use(async (context, next) =>
{
    var accessService = context.RequestServices.GetRequiredService<AccessControlService>();
    var (allowed, status, message) = accessService.Validate(context);
    if (!allowed)
    {
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ProxyServer\"";
        }
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(message);
        return;
    }
    await next();
});

// Proxy middleware with in-memory caching support.
app.Run(async context =>
{
    var targetUrl = settings.UpstreamUrl + context.Request.Path + context.Request.QueryString;
    var requestMessage = new HttpRequestMessage
    {
        Method = new HttpMethod(context.Request.Method),
        RequestUri = new Uri(targetUrl)
    };

    // Copy request headers (except "Host").
    foreach (var header in context.Request.Headers)
    {
        if (!string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    // Copy request body if needed.
    if (!HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method) &&
        !HttpMethods.IsDelete(context.Request.Method) &&
        !HttpMethods.IsTrace(context.Request.Method))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        var contentType = context.Request.Headers["Content-Type"];
        if (!string.IsNullOrEmpty(contentType))
            requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", (string)contentType);
    }

    // Determine if caching is applicable.
    IMemoryCache? cache = null;
    if (settings.UseMemoryCache)
    {
        cache = context.RequestServices.GetService<IMemoryCache>();
    }
    string? cacheKey = null;
    bool canCache = false;
    // Enable caching only for GET/POST and non-file requests.
    if (cache != null &&
        (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
         context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrEmpty(context.Request.ContentType) ||
         !context.Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)))
    {
        canCache = true;
        // Compute cache key: Method, Path, Query and (if applicable) hash of request body.
        cacheKey = $"{context.Request.Method}:{context.Request.Path}{context.Request.QueryString}";
        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            context.Request.EnableBuffering();
            using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
            {
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));
                cacheKey += ":" + Convert.ToHexString(hash);
            }
        }
    }

    // Try to serve response from cache.
    if (canCache && cache != null && cache.TryGetValue(cacheKey, out CachedResponse cached))
    {
        context.Response.StatusCode = cached.StatusCode;
        foreach (var header in cached.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }
        await context.Response.Body.WriteAsync(cached.Body);
        return;
    }

    var httpClient = new HttpClient();
    HttpResponseMessage upstreamResponse;
    try
    {
        upstreamResponse = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted
        );
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsync($"Bad Gateway: {ex.Message}");
        return;
    }

    context.Response.StatusCode = (int)upstreamResponse.StatusCode;
    // Copy upstream response headers.
    var responseHeaders = new Dictionary<string, string[]>();
    foreach (var header in upstreamResponse.Headers)
    {
        var values = header.Value.ToArray();
        context.Response.Headers[header.Key] = values;
        responseHeaders[header.Key] = values;
    }
    foreach (var header in upstreamResponse.Content.Headers)
    {
        var values = header.Value.ToArray();
        context.Response.Headers[header.Key] = values;
        responseHeaders[header.Key] = values;
    }
    // Remove encoding headers to avoid double encoding.
    context.Response.Headers.Remove("transfer-encoding");
    context.Response.Headers.Remove("content-encoding");

    // Read response content.
    var responseBody = await upstreamResponse.Content.ReadAsByteArrayAsync();
    await context.Response.Body.WriteAsync(responseBody);

    // Cache the response if applicable.
    if (canCache && cache != null)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(settings.CacheDurationSeconds))
            .SetSize(1); // Each cache entry counts as 1.
        var cachedResponse = new CachedResponse((int)upstreamResponse.StatusCode, responseHeaders, responseBody);
        cache.Set(cacheKey, cachedResponse, cacheEntryOptions);
    }
});

app.Run();
