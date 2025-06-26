using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Middleware;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DimonSmart.ProxyServer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds proxy services to the service collection with composition pattern configuration.
    /// Cache services are composed as nested dependencies:
    /// - No cache: null (no caching)
    /// - Memory only: MemoryCacheService (terminal)
    /// - Database only: DatabaseCacheService (terminal)
    /// - Memory + Database: MemoryCacheService wrapping DatabaseCacheService
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
            return new DatabaseCacheService(settings.DiskCache.CachePath, logger);
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
            // Case 1: Both memory and database enabled - Memory wrapping database
            if (hasMemoryCache && hasDiskCache)
            {
                var diskCache = provider.GetRequiredService<IExtendedCacheService>();
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<MemoryCacheService>>();
                return new MemoryCacheService(memoryCache, settings, logger, diskCache);
            }

            // Case 2: Only database enabled
            if (hasDiskCache)
            {
                return provider.GetRequiredService<IExtendedCacheService>();
            }

            // Case 3: Only memory enabled
            if (hasMemoryCache)
            {
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<MemoryCacheService>>();
                return new MemoryCacheService(memoryCache, settings, logger);
            }

            // Case 4: No cache enabled
            return new NullCacheService();
        });
    }
}
