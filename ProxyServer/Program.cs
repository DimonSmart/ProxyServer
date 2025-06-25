using System.Text.Json;
using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Extensions;

const string SettingsFileName = "settings.json";

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
