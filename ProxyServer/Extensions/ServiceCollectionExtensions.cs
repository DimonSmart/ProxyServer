using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Middleware;
using DimonSmart.ProxyServer.Services;

namespace DimonSmart.ProxyServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProxyServices(this IServiceCollection services, ProxySettings settings)
    {
        if (settings.UseMemoryCache)
        {
            services.AddMemoryCache(options =>
            {
                if (settings.CacheMaxEntries > 0)
                {
                    options.SizeLimit = settings.CacheMaxEntries;
                }
            });
        }

        services.AddSingleton(settings);
        services.AddSingleton<IAccessControlService, AccessControlService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();
        services.AddScoped<ExceptionHandlingMiddleware>();
        services.AddHttpClient<IProxyService, ProxyService>();

        // Add controller support for health check endpoints
        services.AddControllers();

        return services;
    }
}
