using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;

namespace ProxyServer.FunctionalTests.TestServer;

/// <summary>
/// Builder for creating a test HTTP server that provides string reversal functionality
/// </summary>
public class TestServerHostBuilder
{
    /// <summary>
    /// Creates and configures a test server instance
    /// </summary>
    /// <param name="port">The port number for the test server to listen on</param>
    /// <returns>A configured host for the test server</returns>
    public static IHost CreateTestServer(int port)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseUrls($"http://localhost:{port}")
                    .ConfigureServices(services =>
                    {
                        services.AddControllers();
                        services.AddLogging();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });
            });

        return builder.Build();
    }
}
