using System.Text.Json;
using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Extensions;
using DimonSmart.ProxyServer.Services;

const string SettingsFileName = "settings.json";

// Handle command line arguments
if (args.Length > 0)
{
    var exitCode = await HandleCommandLineAsync(args);
    Environment.Exit(exitCode);
}

var settings = LoadSettings();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProxyServices(settings);
ConfigureWebHost(builder.WebHost, settings);

var app = builder.Build();

app.UseProxyServer();

// Log the proxy server information on startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== Proxy Server Started ===");
    logger.LogInformation("Proxy URL: http://localhost:{Port}", settings.Port);
    logger.LogInformation("Upstream URL: {UpstreamUrl}", settings.UpstreamUrl);
    logger.LogInformation("Open http://localhost:{Port}/ in your browser to access the proxied server", settings.Port);
    logger.LogInformation("============================");
});

app.Run();

static async Task<int> HandleCommandLineAsync(string[] args)
{
    var settings = LoadSettings();

    // Create a temporary service collection for command line operations
    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole());
    services.AddSingleton(settings);
    services.AddProxyServices(settings);
    services.AddTransient<CommandLineService>();

#pragma warning disable ASP0000 // BuildServiceProvider is acceptable for command line scenarios
    using var serviceProvider = services.BuildServiceProvider();
#pragma warning restore ASP0000

    var commandLineService = serviceProvider.GetRequiredService<CommandLineService>();
    return await commandLineService.ExecuteAsync(args);
}

static ProxySettings LoadSettings()
{
    return File.Exists(SettingsFileName)
        ? JsonSerializer.Deserialize<ProxySettings>(File.ReadAllText(SettingsFileName))!
        : new ProxySettings();
}

static void ConfigureWebHost(IWebHostBuilder webHost, ProxySettings settings)
{
    webHost.ConfigureKestrel(options =>
    {
        // Listen on IPv4
        options.Listen(System.Net.IPAddress.Loopback, settings.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.CertificatePassword))
            {
                listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            }
        });

        // Listen on IPv6
        options.Listen(System.Net.IPAddress.IPv6Loopback, settings.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.CertificatePassword))
            {
                listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            }
        });
    });
}
