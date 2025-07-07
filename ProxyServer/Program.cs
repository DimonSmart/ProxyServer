using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Extensions;
using DimonSmart.ProxyServer.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Text.Json;

const string SettingsFileName = "settings.json";

if (args.Length > 0)
{
    var exitCode = await HandleCommandLineAsync(args);
    Environment.Exit(exitCode);
}

var settings = LoadSettings();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddProxyServices(settings);
ConfigureWebHost(builder.WebHost, settings);

var app = builder.Build();
app.UseProxyServer();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== Proxy Server Started ===");

    bool hasHttps = settings.HttpsPort.HasValue;
    string iface = settings.ListenOnAllInterfaces ? "0.0.0.0" : "localhost";
    var urls = new List<(string Scheme, string Url, string Note)>
    {
        ("HTTP",  $"http://{iface}:{settings.Port}", settings.ListenOnAllInterfaces ? "(all interfaces)" : "(localhost only)")
    };
    if (hasHttps)
        urls.Add(("HTTPS", $"https://{iface}:{settings.HttpsPort}", settings.ListenOnAllInterfaces ? "(all interfaces)" : "(localhost only)"));

    foreach (var (scheme, url, note) in urls)
        logger.LogInformation("{Scheme} URL: {Url} {Note}", scheme, url, note);

    logger.LogInformation("Upstream URL: {UpstreamUrl}", settings.UpstreamUrl);

    if (hasHttps)
    {
        logger.LogInformation("HTTPS: ENABLED with development certificate");
    }
    else
    {
        logger.LogInformation("HTTPS: DISABLED - Using HTTP only");
    }

    if (settings.AllowedCredentials?.Count > 0)
    {
        logger.LogInformation("Access Control: ENABLED");
        foreach (var (cred, idx) in settings.AllowedCredentials.Select((c, i) => (c, i + 1)))
        {
            var ips = cred.IPs?.Any() == true ? string.Join(", ", cred.IPs) : "ANY";
            var pwds = cred.Passwords?.Count ?? 0;
            logger.LogInformation("  Set {Idx}: IPs=[{IPs}], Passwords={Count}", idx, ips, pwds);
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

static ProxySettings LoadSettings()
    => File.Exists(SettingsFileName)
        ? JsonSerializer.Deserialize<ProxySettings>(File.ReadAllText(SettingsFileName))!
        : new ProxySettings();

static void ConfigureWebHost(IWebHostBuilder webHost, ProxySettings settings)
{
    var addresses = settings.ListenOnAllInterfaces
        ? new[] { System.Net.IPAddress.Any, System.Net.IPAddress.IPv6Any }
        : new[] { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback };

    webHost.ConfigureKestrel(opts =>
    {
        foreach (var addr in addresses)
        {
            opts.Listen(addr, settings.Port);
            if (settings.HttpsPort.HasValue)
                opts.Listen(addr, settings.HttpsPort.Value, lo => ConfigureHttps(lo, settings));
        }
    });
}

static void ConfigureHttps(ListenOptions lo, ProxySettings settings)
{
    // Create minimal services for certificate loading
    var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
    var sslService = new SslCertificateService(settings, LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SslCertificateService>());

    // Try to load certificate from settings
    var certificate = sslService.LoadCertificate();
    
    if (certificate == null)
    {
        logger.LogInformation("No valid certificate found, creating self-signed certificate");
        certificate = sslService.CreateSelfSignedCertificate();
    }

    lo.UseHttps(certificate);
    
    logger.LogInformation("HTTPS configured with certificate: {Subject}", certificate.Subject);
}

static async Task<int> HandleCommandLineAsync(string[] args)
{
    var settings = LoadSettings();
    var services = new ServiceCollection();
    services.AddLogging(b => b.AddConsole());
    services.AddSingleton(settings);
    services.AddProxyServices(settings);
    services.AddTransient<CommandLineService>();

    // Note: BuildServiceProvider is acceptable here as this is a one-time CLI operation
#pragma warning disable ASP0000
    using var sp = services.BuildServiceProvider();
#pragma warning restore ASP0000
    return await sp.GetRequiredService<CommandLineService>().ExecuteAsync(args);
}
