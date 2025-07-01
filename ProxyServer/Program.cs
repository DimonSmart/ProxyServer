using System.Text.Json;
using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Extensions;
using DimonSmart.ProxyServer.Services;

const string SettingsFileName = "settings.json";

if (args.Length > 0)
{
    var exitCode = await HandleCommandLineAsync(args);
    Environment.Exit(exitCode);
}

var settings = LoadSettings();
var builder = WebApplication.CreateBuilder(args);

// Configure detailed logging for access control
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddProxyServices(settings);
ConfigureWebHost(builder.WebHost, settings);

var app = builder.Build();

app.UseProxyServer();

// Log the proxy server information on startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== Proxy Server Started ===");

    if (settings.ListenOnAllInterfaces)
    {
        logger.LogInformation("Proxy URL: http://0.0.0.0:{Port} (listening on all interfaces)", settings.Port);
        logger.LogInformation("Local access: http://localhost:{Port}", settings.Port);
    }
    else
    {
        logger.LogInformation("Proxy URL: http://localhost:{Port} (localhost only)", settings.Port);
    }

    logger.LogInformation("Upstream URL: {UpstreamUrl}", settings.UpstreamUrl);

    // Log access control configuration
    if (settings.AllowedCredentials?.Count > 0)
    {
        logger.LogInformation("Access Control: ENABLED");
        foreach (var (credential, index) in settings.AllowedCredentials.Select((c, i) => (c, i)))
        {
            var ips = credential.IPs?.Count > 0 ? string.Join(", ", credential.IPs) : "ANY";
            var passwordCount = credential.Passwords?.Count ?? 0;
            logger.LogInformation("  Credential Set {Index}: IPs=[{IPs}], Passwords={PasswordCount}",
                index + 1, ips, passwordCount);
        }
    }
    else
    {
        logger.LogWarning("Access Control: DISABLED - All connections allowed");
    }

    logger.LogInformation("Open the proxy URL in your browser to access the proxied server");
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
        var ipv4Address = settings.ListenOnAllInterfaces ? System.Net.IPAddress.Any : System.Net.IPAddress.Loopback;
        var ipv6Address = settings.ListenOnAllInterfaces ? System.Net.IPAddress.IPv6Any : System.Net.IPAddress.IPv6Loopback;

        options.Listen(ipv4Address, settings.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.CertificatePassword))
            {
                listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            }
        });

        options.Listen(ipv6Address, settings.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.CertificatePassword))
            {
                listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            }
        });
    });
}
