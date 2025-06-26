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
        services.AddScoped<ExceptionHandlingMiddleware>();
        services.AddHttpClient<IProxyService, ProxyService>();
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
            if (settings.CacheMaxEntries > 0)
            {
                options.SizeLimit = settings.CacheMaxEntries;
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
            // Both memory and disk             // Both memory and disk cache enabled - Composable cache with memory as primary and disk as fallback and disk as fallback
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
