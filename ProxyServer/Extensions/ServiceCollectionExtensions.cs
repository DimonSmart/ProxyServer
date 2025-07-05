using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Middleware;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds proxy services to the service collection with composition pattern configuration.
    /// Cache services are composed using ComposableCacheService:
    /// - No cache: NullCacheService
    /// - Memory only: MemoryCacheService
    /// - Database only: DatabaseCacheService
    /// - Memory + Database: ComposableCacheService(MemoryCacheService, DatabaseCacheService)
    /// </summary>
    public static IServiceCollection AddProxyServices(this IServiceCollection services, ProxySettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IAccessControlService, AccessControlService>();
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();
        services.AddSingleton<ICachePolicyService, CachePolicyService>();
        services.AddSingleton<ICacheKeyService, CacheKeyService>();
        services.AddSingleton<IResponseWriterService, ResponseWriterService>();
        services.AddSingleton<IConnectionDiagnosticsService, ConnectionDiagnosticsService>();
        services.AddSingleton<ISslCertificateService, SslCertificateService>();
        services.AddSingleton<CacheDumpService>();
        services.AddSingleton<CommandLineService>();
        services.AddScoped<ExceptionHandlingMiddleware>();

        // Configure HttpClient with SSL settings and diagnostics
        services.AddHttpClient<IProxyService, ProxyService>(client =>
        {
            client.Timeout = settings.UpstreamTimeoutSeconds > 0
                ? TimeSpan.FromSeconds(settings.UpstreamTimeoutSeconds)
                : Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler()
            {
                UseCookies = false,
                AllowAutoRedirect = false
            };

            // Configure SSL/TLS settings for upstream connections
            if (!settings.Ssl.ValidateUpstreamCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Note: This will be logged in console since we don't have DI context here
                    if (settings.Ssl.EnableSslDebugging && certificate != null)
                    {
                        Console.WriteLine($"[SSL DEBUG] Certificate validation bypassed for upstream connection");
                        Console.WriteLine($"[SSL DEBUG] Certificate Subject: {certificate.Subject}");
                        Console.WriteLine($"[SSL DEBUG] SSL Policy Errors: {sslPolicyErrors}");
                    }
                    return true; // Accept all certificates
                };
            }

            handler.SslProtocols = settings.Ssl.AllowedSslProtocols;

            return handler;
        });

        services.AddControllers();

        ConfigureMemoryCacheIfEnabled(services, settings);
        ConfigureDiskCacheIfEnabled(services, settings);
        ConfigureCacheServiceWithDecorators(services, settings);

        return services;
    }

    private static void ConfigureMemoryCacheIfEnabled(IServiceCollection services, ProxySettings settings)
    {
        if (!settings.EnableMemoryCache) return;

        services.AddMemoryCache(options =>
        {
            if (settings.MemoryCache.MaxEntries > 0)
            {
                options.SizeLimit = settings.MemoryCache.MaxEntries;
            }
        });
    }

    private static void ConfigureDiskCacheIfEnabled(IServiceCollection services, ProxySettings settings)
    {
        if (!settings.EnableDiskCache) return;

        // Register disk cache service as IExtendedCacheService
        services.AddSingleton<IExtendedCacheService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DatabaseCacheService>>();
            return new DatabaseCacheService(settings.DiskCache.CachePath, settings, logger);
        });

        // Register cache cleanup service
        services.AddHostedService<CacheCleanupService>();
    }

    private static void ConfigureCacheServiceWithDecorators(IServiceCollection services, ProxySettings settings)
    {
        var hasMemoryCache = settings.EnableMemoryCache;
        var hasDiskCache = settings.EnableDiskCache;

        services.AddSingleton<ICacheService>(provider =>
        {
            // Both memory and disk cache enabled - Composable cache with memory as primary and disk as fallback
            if (hasMemoryCache && hasDiskCache)
            {
                var diskCache = provider.GetRequiredService<IExtendedCacheService>();
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<MemoryCacheService>>();
                var memoryCacheService = new MemoryCacheService(memoryCache, logger);
                return new ComposableCacheService(memoryCacheService, diskCache);
            }

            // Only disk cache enabled
            if (hasDiskCache)
            {
                return provider.GetRequiredService<IExtendedCacheService>();
            }

            // Only memory cache enabled
            if (hasMemoryCache)
            {
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<MemoryCacheService>>();
                return new MemoryCacheService(memoryCache, logger);
            }

            // No cache enabled
            return new NullCacheService();
        });
    }
}
