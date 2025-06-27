using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Models;
using System.Text.Json;

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
            "dump" => await DumpCacheAsync(args.Skip(1).ToArray()),
            _ => ShowUnknownCommand(command)
        };
    }

    private static int ShowHelp()
    {
        Console.WriteLine("ProxyServer - Caching Proxy Server");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help           Show this help message");
        Console.WriteLine("  config         Show configuration information");
        Console.WriteLine("  clear-cache    Clear all cached data");
        Console.WriteLine("  dump           Dump cache contents to JSON");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ProxyServer help");
        Console.WriteLine("  ProxyServer config");
        Console.WriteLine("  ProxyServer clear-cache");
        Console.WriteLine("  ProxyServer dump");
        Console.WriteLine("  ProxyServer dump output.json");
        Console.WriteLine("  ProxyServer dump output.json filter_text");
        return 0;
    }

    private int ShowConfig()
    {
        Console.WriteLine("ProxyServer Configuration");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine($"Settings file location: {GetSettingsPath()}");
        Console.WriteLine($"Memory cache enabled: {_settings.EnableMemoryCache}");
        Console.WriteLine($"Memory cache enabled: {_settings.EnableMemoryCache}");

        if (_settings.EnableMemoryCache)
        {
            Console.WriteLine($"Memory cache TTL: {_settings.MemoryCache.TtlSeconds} seconds");
            Console.WriteLine($"Memory cache max entries: {_settings.MemoryCache.MaxEntries}");
        }

        Console.WriteLine($"Disk cache enabled: {_settings.EnableDiskCache}");

        if (_settings.EnableDiskCache)
        {
            Console.WriteLine($"Disk cache path: {_settings.DiskCache.CachePath}");
            Console.WriteLine($"Disk cache TTL: {_settings.DiskCache.TtlSeconds} seconds");
            Console.WriteLine($"Disk cache max size: {_settings.DiskCache.MaxSizeMB} MB");
            Console.WriteLine($"Cleanup interval: {_settings.DiskCache.CleanupIntervalMinutes} minutes");
        }
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

    private async Task<int> DumpCacheAsync(string[] args)
    {
        try
        {
            string? outputFile = null;
            string? filter = null;

            // Parse arguments
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                outputFile = args[0];
            }

            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                filter = args[1];
            }

            // Check if cache service supports extended operations
            if (_cacheService is not IExtendedCacheService extendedCache)
            {
                Console.WriteLine("Cache service does not support dump operations.");
                return 1;
            }

            Console.WriteLine("Retrieving cache entries...");
            var entries = await extendedCache.GetAllEntriesAsync(filter);

            var dump = new CacheDump
            {
                DumpTimestamp = DateTimeOffset.UtcNow,
                TotalEntries = entries.Count,
                FilteredEntries = entries.Count,
                Filter = filter,
                Entries = entries
            };

            // Configure JSON options for better readability
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserve Unicode characters
            };

            var json = JsonSerializer.Serialize(dump, jsonOptions);

            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, json, System.Text.Encoding.UTF8);
                Console.WriteLine($"Cache dump written to: {Path.GetFullPath(outputFile)}");
            }
            else
            {
                Console.WriteLine(json);
            }

            Console.WriteLine($"Total entries: {entries.Count}");
            if (!string.IsNullOrWhiteSpace(filter))
            {
                Console.WriteLine($"Filter applied: {filter}");
            }

            _logger.LogInformation("Cache dump completed - {Count} entries, filter: {Filter}, output: {Output}",
                entries.Count, filter ?? "none", outputFile ?? "console");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dumping cache: {ex.Message}");
            _logger.LogError(ex, "Error dumping cache via command line");
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
