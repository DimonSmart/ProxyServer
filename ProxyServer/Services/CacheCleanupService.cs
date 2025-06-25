using DimonSmart.ProxyServer.Interfaces;

namespace DimonSmart.ProxyServer.Services;

public class CacheCleanupService(
    IDiskCacheService diskCache,
    ProxySettings settings,
    ILogger<CacheCleanupService> logger) : BackgroundService
{
    private readonly ProxySettings _settings = settings;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableDiskCache)
        {
            logger.LogInformation("Disk cache is disabled, cache cleanup service will not run");
            return;
        }

        var cleanupInterval = TimeSpan.FromMinutes(_settings.DiskCache.CleanupIntervalMinutes);
        logger.LogInformation("Cache cleanup service started with interval: {Interval}", cleanupInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, cancellationToken);

                logger.LogDebug("Starting cache cleanup...");
                await diskCache.CleanupExpiredAsync();

                // Check cache size and warn if it's getting large
                var currentSize = await diskCache.GetSizeAsync();
                var maxSize = _settings.DiskCache.MaxSizeMB * 1024 * 1024; // Convert MB to bytes

                if (currentSize > maxSize * 0.8) // Warn at 80% capacity
                {
                    logger.LogWarning("Cache size ({CurrentMB:F1} MB) is approaching the limit ({MaxMB} MB)",
                        currentSize / (1024.0 * 1024.0), _settings.DiskCache.MaxSizeMB);
                }

                logger.LogDebug("Cache cleanup completed. Current size: {SizeMB:F1} MB",
                    currentSize / (1024.0 * 1024.0));
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cache cleanup");
            }
        }

        logger.LogInformation("Cache cleanup service stopped");
    }
}
