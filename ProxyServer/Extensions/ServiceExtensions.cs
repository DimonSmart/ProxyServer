using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Services;
using DimonSmart.ProxyServer.Middleware;

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
        services.AddHttpClient<IProxyService, ProxyService>();

        return services;
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseProxyServer(this IApplicationBuilder app)
    {
        app.UseMiddleware<AccessControlMiddleware>();
        app.UseMiddleware<ProxyMiddleware>();
        
        return app;
    }
}
