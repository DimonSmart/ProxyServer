using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;

namespace DimonSmart.ProxyServer.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache? _cache;
    private readonly ProxySettings _settings;

    public CacheService(IMemoryCache? cache, ProxySettings settings)
    {
        _cache = cache;
        _settings = settings;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_cache == null) return null;
        
        return await Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : null);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_cache == null) return;

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiration)
            .SetSize(1);

        await Task.Run(() => _cache.Set(key, value, options));
    }

    public string GenerateCacheKey(HttpContext context)
    {
        var cacheKey = $"{context.Request.Method}:{context.Request.Path}{context.Request.QueryString}";
        
        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = reader.ReadToEnd();
            context.Request.Body.Position = 0;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));
            cacheKey += ":" + Convert.ToHexString(hash);
        }

        return cacheKey;
    }

    public bool CanCache(HttpContext context)
    {
        if (!_settings.UseMemoryCache || _cache == null) return false;

        var isGetOrPost = context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                         context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase);

        var isNotFileUpload = string.IsNullOrEmpty(context.Request.ContentType) ||
                             !context.Request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);

        return isGetOrPost && isNotFileUpload;
    }
}
