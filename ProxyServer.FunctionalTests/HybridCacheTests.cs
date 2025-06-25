using DimonSmart.ProxyServer.Interfaces;
using DimonSmart.ProxyServer.Services;
using DimonSmart.ProxyServer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ProxyServer.FunctionalTests;

/// <summary>
/// Tests for the hybrid cache functionality
/// </summary>
[TestFixture]
public class HybridCacheTests
{
    private HybridCacheService _hybridCache = null!;
    private SqliteDiskCacheService _diskCache = null!;
    private IMemoryCache _memoryCache = null!;
    private ProxySettings _settings = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid()}.db");

        _settings = new ProxySettings
        {
            EnableMemoryCache = true,
            EnableDiskCache = true,
            CacheDurationSeconds = 300,
            CacheMaxEntries = 100,
            DiskCache = new DiskCacheSettings
            {
                CachePath = _testDbPath
            }
        };

        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        var diskLogger = new HybridTestLogger<SqliteDiskCacheService>();
        _diskCache = new SqliteDiskCacheService(_testDbPath, diskLogger);

        var hybridLogger = new HybridTestLogger<HybridCacheService>();
        _hybridCache = new HybridCacheService(_memoryCache, _diskCache, _settings, hybridLogger);
    }

    [TearDown]
    public void TearDown()
    {
        _hybridCache?.Dispose();
        _diskCache?.Dispose();
        _memoryCache?.Dispose();

        // Wait a bit for SQLite to release the file handle
        System.Threading.Thread.Sleep(100);

        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch (IOException)
            {
                // If file is still locked, don't fail the test
                // The temp file will be cleaned up by the OS eventually
            }
        }
    }

    [Test]
    public async Task GetAsync_FirstRequest_ReturnsNull()
    {
        // Arrange
        var key = "test-key";

        // Act
        var result = await _hybridCache.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.Null);

        var stats = _hybridCache.GetStatistics();
        Assert.That(stats.TotalRequests, Is.EqualTo(1));
        Assert.That(stats.CacheMisses, Is.EqualTo(1));
        Assert.That(stats.CacheHits, Is.EqualTo(0));
    }

    [Test]
    public async Task SetAsync_ThenGetAsync_ReturnsValueFromHotCache()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        await _hybridCache.SetAsync(key, value, expiration);
        var result = await _hybridCache.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.EqualTo(value));

        var stats = _hybridCache.GetStatistics();
        Assert.That(stats.TotalRequests, Is.EqualTo(1));
        Assert.That(stats.HotCacheHits, Is.EqualTo(1));
        Assert.That(stats.DiskCacheHits, Is.EqualTo(0));
        Assert.That(stats.CacheMisses, Is.EqualTo(0));
    }

    [Test]
    public async Task SetAsync_ClearHotCache_ThenGetAsync_ReturnsValueFromDiskCache()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        await _hybridCache.SetAsync(key, value, expiration);

        // Clear hot cache to simulate memory pressure
        (_memoryCache as MemoryCache)?.Compact(1.0);

        var result = await _hybridCache.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.EqualTo(value));

        var stats = _hybridCache.GetStatistics();
        Assert.That(stats.TotalRequests, Is.EqualTo(1));
        Assert.That(stats.HotCacheHits, Is.EqualTo(0));
        Assert.That(stats.DiskCacheHits, Is.EqualTo(1));
        Assert.That(stats.CacheMisses, Is.EqualTo(0));
    }

    [Test]
    public async Task SetAsync_RestartCache_ThenGetAsync_ReturnsValueFromDiskCache()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var expiration = TimeSpan.FromMinutes(5);

        // Act - Set value and dispose hybrid cache (simulate restart)
        await _hybridCache.SetAsync(key, value, expiration);
        _hybridCache.Dispose();
        _memoryCache.Dispose();

        // Create new instances (simulate restart)
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var diskLogger = new HybridTestLogger<SqliteDiskCacheService>();
        _diskCache = new SqliteDiskCacheService(_testDbPath, diskLogger);
        var hybridLogger = new HybridTestLogger<HybridCacheService>();
        _hybridCache = new HybridCacheService(_memoryCache, _diskCache, _settings, hybridLogger);

        var result = await _hybridCache.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.EqualTo(value));

        var stats = _hybridCache.GetStatistics();
        Assert.That(stats.TotalRequests, Is.EqualTo(1));
        Assert.That(stats.DiskCacheHits, Is.EqualTo(1));
        Assert.That(stats.HotCacheHits, Is.EqualTo(0)); // Fresh memory cache
    }

    [Test]
    public async Task GetAsync_AfterDiskHit_PromatesToHotCache()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        await _hybridCache.SetAsync(key, value, expiration);
        (_memoryCache as MemoryCache)?.Compact(1.0); // Clear hot cache

        // First call hits disk cache
        var result1 = await _hybridCache.GetAsync<string>(key);

        // Second call should hit hot cache (promoted)
        var result2 = await _hybridCache.GetAsync<string>(key);

        // Assert
        Assert.That(result1, Is.EqualTo(value));
        Assert.That(result2, Is.EqualTo(value));

        var stats = _hybridCache.GetStatistics();
        Assert.That(stats.TotalRequests, Is.EqualTo(2));
        Assert.That(stats.DiskCacheHits, Is.EqualTo(1));
        Assert.That(stats.HotCacheHits, Is.EqualTo(1));
        Assert.That(stats.CacheMisses, Is.EqualTo(0));
    }
}

/// <summary>
/// Simple test logger implementation for hybrid cache tests
/// </summary>
public class HybridTestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Silent logger for tests
    }
}
