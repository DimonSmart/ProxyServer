using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

/// <summary>
/// Service for handling command line operations
/// </summary>
public class CommandLineService
{
    private readonly ICacheService _cacheService;
    private readonly ProxySettings _settings;
    private readonly ILogger<CommandLineService> _logger;

    public CommandLineService(
        ICacheService cacheService,
        ProxySettings settings,
        ILogger<CommandLineService> logger)
    {
        _cacheService = cacheService;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Executes the command line arguments
    /// </summary>
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "help" => ShowHelp(),
            "config" => ShowConfig(),
            "clear-cache" => await ClearCacheAsync(),
            _ => ShowUnknownCommand(command)
        };
    }

    private int ShowHelp()
    {
        Console.WriteLine("ProxyServer - Caching Proxy Server");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help           Show this help message");
        Console.WriteLine("  config         Show configuration information");
        Console.WriteLine("  clear-cache    Clear all cached data");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ProxyServer help");
        Console.WriteLine("  ProxyServer config");
        Console.WriteLine("  ProxyServer clear-cache");
        return 0;
    }

    private int ShowConfig()
    {
        Console.WriteLine("ProxyServer Configuration");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine($"Settings file location: {GetSettingsPath()}");
        Console.WriteLine($"Memory cache enabled: {_settings.EnableMemoryCache}");
        Console.WriteLine($"Disk cache enabled: {_settings.EnableDiskCache}");
        
        if (_settings.EnableDiskCache)
        {
            Console.WriteLine($"Disk cache path: {_settings.DiskCache.CachePath}");
            Console.WriteLine($"Disk cache max size: {_settings.DiskCache.MaxSizeMB} MB");
            Console.WriteLine($"Cleanup interval: {_settings.DiskCache.CleanupIntervalMinutes} minutes");
        }
        
        Console.WriteLine($"Cache duration: {_settings.CacheDurationSeconds} seconds");
        Console.WriteLine($"Listen port: {_settings.Port}");
        Console.WriteLine($"Upstream URL: {_settings.UpstreamUrl}");
        return 0;
    }

    private async Task<int> ClearCacheAsync()
    {
        try
        {
            Console.WriteLine("Clearing cache...");
            await _cacheService.ClearAsync();
            Console.WriteLine("Cache cleared successfully.");
            _logger.LogInformation("Cache cleared via command line");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing cache: {ex.Message}");
            _logger.LogError(ex, "Error clearing cache via command line");
            return 1;
        }
    }

    private int ShowUnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Use 'ProxyServer help' to see available commands.");
        return 1;
    }

    private string GetSettingsPath()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        return Path.GetFullPath(settingsPath);
    }
}
