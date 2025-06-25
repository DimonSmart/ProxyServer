using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace DimonSmart.ProxyServer.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseProxyServer(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseRouting();
        app.UseMiddleware<AccessControlMiddleware>();

        // Map controller endpoints (health checks) before proxy
        app.UseEndpoints(endpoints =>
        {
            // Map specific controller routes first
            endpoints.MapGet("/health", async context =>
            {
                var cacheService = context.RequestServices.GetRequiredService<ICacheService>();
                var settings = context.RequestServices.GetRequiredService<ProxySettings>();

                var healthController = new Controllers.HealthController(cacheService, settings);
                var result = healthController.GetHealth();

                if (result.Result is OkObjectResult okResult)
                {
                    await context.Response.WriteAsJsonAsync(okResult.Value);
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(result.Value);
                }
            });

            endpoints.MapGet("/ping", async context =>
            {
                var cacheService = context.RequestServices.GetRequiredService<ICacheService>();
                var settings = context.RequestServices.GetRequiredService<ProxySettings>();

                var healthController = new Controllers.HealthController(cacheService, settings);
                var result = healthController.Ping();

                if (result.Result is OkObjectResult okResult)
                {
                    await context.Response.WriteAsJsonAsync(okResult.Value);
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(result.Value);
                }
            });

            endpoints.MapGet("/stats/cache", async context =>
            {
                var cacheService = context.RequestServices.GetRequiredService<ICacheService>();
                var settings = context.RequestServices.GetRequiredService<ProxySettings>();

                var healthController = new Controllers.HealthController(cacheService, settings);
                var result = healthController.GetCacheStats();

                if (result.Result is OkObjectResult okResult)
                {
                    await context.Response.WriteAsJsonAsync(okResult.Value);
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(result.Value);
                }
            });

            // For any request that doesn't match above, use the proxy middleware
            endpoints.Map("{**catch-all}", async context =>
            {
                var proxyService = context.RequestServices.GetRequiredService<IProxyService>();
                var cacheService = context.RequestServices.GetRequiredService<ICacheService>();
                var settings = context.RequestServices.GetRequiredService<ProxySettings>();

                var proxyMiddleware = new ProxyMiddleware(
                    async (ctx) => await Task.CompletedTask,
                    proxyService,
                    cacheService,
                    settings);

                await proxyMiddleware.InvokeAsync(context);
            });
        });

        return app;
    }
}
