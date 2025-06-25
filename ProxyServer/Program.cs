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
        options.ListenAnyIP(settings.Port, listenOptions =>
        {
            if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.CertificatePassword))
            {
                listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            }
        });
    });
}
