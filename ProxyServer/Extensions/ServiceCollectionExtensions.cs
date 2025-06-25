using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Middleware;
using DimonSmart.ProxyServer.Services;

namespace DimonSmart.ProxyServer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds proxy services to the service collection with linear configuration based on cache settings.
    /// All combinations of EnableMemoryCache and EnableDiskCache are valid:
    /// - Both false: No caching
    /// - EnableMemoryCache only: Memory-only cache
    /// - EnableDiskCache only: Disk-only cache
    /// - Both true: Hybrid cache (memory + disk)
    /// </summary>
    public static IServiceCollection AddProxyServices(this IServiceCollection services, ProxySettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IAccessControlService, AccessControlService>();
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();
        services.AddScoped<ExceptionHandlingMiddleware>();
        services.AddHttpClient<IProxyService, ProxyService>();
        services.AddControllers();

        ConfigureMemoryCacheIfEnabled(services, settings);
        ConfigureDiskCacheIfEnabled(services, settings);

        // Configure the main cache service based on enabled cache types
        ConfigureCacheService(services, settings);

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

        // Register disk cache service
        services.AddSingleton<IDiskCacheService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SqliteDiskCacheService>>();
            return new SqliteDiskCacheService(settings.DiskCache.CachePath, logger);
        });

        // Register cache cleanup service
        services.AddHostedService<CacheCleanupService>();
    }

    private static void ConfigureCacheService(IServiceCollection services, ProxySettings settings)
    {
        var hasMemoryCache = settings.EnableMemoryCache;
        var hasDiskCache = settings.EnableDiskCache;

        // Hybrid cache: both memory and disk enabled
        if (hasMemoryCache && hasDiskCache)
        {
            services.AddSingleton<ICacheService, HybridCacheService>();
            return;
        }

        // Disk-only cache: only disk enabled
        if (hasDiskCache)
        {
            services.AddSingleton<ICacheService>(provider =>
            {
                var diskCache = provider.GetRequiredService<IDiskCacheService>();
                var logger = provider.GetRequiredService<ILogger<DiskOnlyCacheService>>();
                return new DiskOnlyCacheService(diskCache, settings, logger);
            });
            return;
        }

        // Memory-only cache: only memory enabled or neither enabled (fallback to memory)
        services.AddSingleton<ICacheService, CacheService>();
    }
}
