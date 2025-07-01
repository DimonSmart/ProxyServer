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
    private readonly IServiceProvider _serviceProvider;
    private readonly ProxySettings _settings;
    private readonly ILogger<CommandLineService> _logger;
    private readonly CacheDumpService _cacheDumpService;

    public CommandLineService(
        ICacheService cacheService,
        IServiceProvider serviceProvider,
        ProxySettings settings,
        ILogger<CommandLineService> logger,
        CacheDumpService cacheDumpService)
    {
        _cacheService = cacheService;
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
        _cacheDumpService = cacheDumpService;
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
            "dump-detailed" => await DumpDetailedCacheAsync(args.Skip(1).ToArray()),
            "analyze-cache" => await AnalyzeCacheAsync(args.Skip(1).ToArray()),
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
        Console.WriteLine("  dump-detailed  Dump cache contents with decoded requests/responses");
        Console.WriteLine("  analyze-cache  Analyze cache entries with compact preview");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ProxyServer help");
        Console.WriteLine("  ProxyServer config");
        Console.WriteLine("  ProxyServer clear-cache");
        Console.WriteLine("  ProxyServer dump");
        Console.WriteLine("  ProxyServer dump output.json");
        Console.WriteLine("  ProxyServer dump output.json filter_text");
        Console.WriteLine("  ProxyServer dump-detailed");
        Console.WriteLine("  ProxyServer dump-detailed detailed_output.json");
        Console.WriteLine("  ProxyServer dump-detailed detailed_output.json llama");
        Console.WriteLine("  ProxyServer analyze-cache");
        Console.WriteLine("  ProxyServer analyze-cache qwen");
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

            // Try to get the disk cache service for dump operation
            var extendedCacheService = _serviceProvider.GetService<IExtendedCacheService>();
            if (extendedCacheService == null)
            {
                Console.WriteLine("Disk cache is not enabled. Dump operation requires disk cache to be enabled.");
                Console.WriteLine("Please enable disk cache in settings.json by setting 'EnableDiskCache': true");
                return 1;
            }

            Console.WriteLine("Retrieving cache entries from disk cache...");
            var entries = await extendedCacheService.GetAllEntriesAsync(filter);

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

    private async Task<int> DumpDetailedCacheAsync(string[] args)
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

            // Try to get the disk cache service for dump operation
            var extendedCacheService = _serviceProvider.GetService<IExtendedCacheService>();
            if (extendedCacheService == null)
            {
                Console.WriteLine("Disk cache is not enabled. Detailed dump operation requires disk cache to be enabled.");
                Console.WriteLine("Please enable disk cache in settings.json by setting 'EnableDiskCache': true");
                return 1;
            }

            Console.WriteLine("Retrieving and decoding cache entries from disk cache...");
            var entries = await extendedCacheService.GetAllEntriesAsync(filter);
            var dump = _cacheDumpService.CreateDetailedDump(entries, filter);

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
                Console.WriteLine($"Detailed cache dump written to: {Path.GetFullPath(outputFile)}");
            }
            else
            {
                Console.WriteLine(json);
            }

            // Show summary
            Console.WriteLine();
            Console.WriteLine("=== Cache Summary ===");
            Console.WriteLine($"Total entries: {dump.TotalEntries}");
            if (!string.IsNullOrWhiteSpace(filter))
            {
                Console.WriteLine($"Filter applied: {filter}");
            }
            Console.WriteLine($"Total cache size: {dump.Statistics.TotalSizeBytes:N0} bytes");
            Console.WriteLine($"Expired entries: {dump.Statistics.ExpiredEntries}");

            if (dump.Statistics.StatusCodes.Any())
            {
                Console.WriteLine("Status codes:");
                foreach (var statusCode in dump.Statistics.StatusCodes.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  {statusCode.Key}: {statusCode.Value} entries");
                }
            }

            if (dump.Statistics.ContentTypes.Any())
            {
                Console.WriteLine("Content types:");
                foreach (var contentType in dump.Statistics.ContentTypes.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  {contentType.Key}: {contentType.Value} entries");
                }
            }

            _logger.LogInformation("Detailed cache dump completed - {Count} entries, filter: {Filter}, output: {Output}",
                dump.TotalEntries, filter ?? "none", outputFile ?? "console");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dumping detailed cache: {ex.Message}");
            _logger.LogError(ex, "Error dumping detailed cache via command line");
            return 1;
        }
    }

    private async Task<int> AnalyzeCacheAsync(string[] args)
    {
        try
        {
            string? filter = null;

            // Parse arguments
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                filter = args[0];
            }

            // Try to get the disk cache service for analysis
            var extendedCacheService = _serviceProvider.GetService<IExtendedCacheService>();
            if (extendedCacheService == null)
            {
                Console.WriteLine("Disk cache is not enabled. Cache analysis requires disk cache to be enabled.");
                Console.WriteLine("Please enable disk cache in settings.json by setting 'EnableDiskCache': true");
                return 1;
            }

            Console.WriteLine("Analyzing cache entries...");
            var entries = await extendedCacheService.GetAllEntriesAsync(filter);
            var dump = _cacheDumpService.CreateDetailedDump(entries, filter);

            Console.WriteLine();
            Console.WriteLine("=== Cache Analysis ===");
            Console.WriteLine($"Total entries: {dump.TotalEntries}");
            if (!string.IsNullOrWhiteSpace(filter))
            {
                Console.WriteLine($"Filter applied: {filter}");
            }
            Console.WriteLine($"Total cache size: {dump.Statistics.TotalSizeBytes:N0} bytes");
            Console.WriteLine($"Expired entries: {dump.Statistics.ExpiredEntries}");
            Console.WriteLine();

            if (dump.Entries.Any())
            {
                Console.WriteLine("Cache Entries:");
                Console.WriteLine("──────────────────────────────────────────────────────────────────────────────");

                foreach (var entry in dump.Entries.Take(20)) // Show first 20 entries
                {
                    Console.WriteLine($"Hash: {entry.CacheKeyHash}");
                    Console.WriteLine($"Status: {entry.Response.StatusCode}");
                    Console.WriteLine($"Content-Type: {entry.Response.ContentType ?? "Unknown"}");
                    Console.WriteLine($"Size: {entry.Response.BodySizeBytes:N0} bytes");
                    Console.WriteLine($"Streamed: {entry.Response.WasStreamed}");
                    Console.WriteLine($"Created: {entry.Metadata.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Expires: {entry.Metadata.ExpiresAt:yyyy-MM-dd HH:mm:ss} (TTL: {entry.Metadata.TtlRemainingSeconds:F0}s)");

                    // Show headers that might affect caching
                    var interestingHeaders = new[] { "Date", "Server", "X-Request-Id", "Request-Id", "Trace-Id" };
                    var foundHeaders = entry.Response.Headers
                        .Where(h => interestingHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    if (foundHeaders.Any())
                    {
                        Console.WriteLine("Headers that might affect caching:");
                        foreach (var header in foundHeaders)
                        {
                            Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                        }
                    }

                    // Show response body preview
                    if (!string.IsNullOrEmpty(entry.Response.BodyPreview))
                    {
                        Console.WriteLine($"Response preview: {entry.Response.BodyPreview}");
                    }

                    Console.WriteLine("──────────────────────────────────────────────────────────────────────────────");
                }

                if (dump.Entries.Count > 20)
                {
                    Console.WriteLine($"... and {dump.Entries.Count - 20} more entries");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== Statistics ===");

            if (dump.Statistics.StatusCodes.Any())
            {
                Console.WriteLine("Status codes:");
                foreach (var statusCode in dump.Statistics.StatusCodes.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  {statusCode.Key}: {statusCode.Value} entries");
                }
            }

            if (dump.Statistics.ContentTypes.Any())
            {
                Console.WriteLine("Content types:");
                foreach (var contentType in dump.Statistics.ContentTypes.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  {contentType.Key}: {contentType.Value} entries");
                }
            }

            // Show cache key analysis
            Console.WriteLine();
            Console.WriteLine("=== Cache Key Analysis ===");
            Console.WriteLine("Note: Cache keys are hashed for security. To debug cache misses:");
            Console.WriteLine("1. Check if timestamps in responses change between requests");
            Console.WriteLine("2. Look for request IDs or session IDs in headers/body");
            Console.WriteLine("3. Verify query parameters are identical");
            Console.WriteLine("4. Check request body content for dynamic fields");

            _logger.LogInformation("Cache analysis completed - {Count} entries, filter: {Filter}",
                dump.TotalEntries, filter ?? "none");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing cache: {ex.Message}");
            _logger.LogError(ex, "Error analyzing cache via command line");
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
