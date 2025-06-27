using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Models;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ProxyServer.FunctionalTests;

[TestFixture]
public class DebugCacheTest
{
    private DatabaseCacheService _cacheService = null!;
    private ProxySettings _settings = null!;
    private ILogger<DatabaseCacheService> _logger = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"debug_cache_{Guid.NewGuid():N}.db");
        _settings = new ProxySettings();
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DatabaseCacheService>();
        _cacheService = new DatabaseCacheService(_testDbPath, _settings, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _cacheService?.Dispose();

        // Give the system a moment to release the database file
        Thread.Sleep(100);

        // Try to delete the test database file with retries
        if (File.Exists(_testDbPath))
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (i < 4)
                {
                    // File might still be locked, wait a bit and retry
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Info: Could not delete test database file '{_testDbPath}' because {ex.Message}");
                    break;
                }
            }
        }
    }

    [Test]
    public async Task SimpleTest_SetAndGet()
    {
        // Arrange
        var testResponse = new CachedResponse(
            StatusCode: 200,
            Headers: new Dictionary<string, string[]> { { "Content-Type", new[] { "text/plain" } } },
            Body: System.Text.Encoding.UTF8.GetBytes("Test content")
        );

        // Act - Set data
        await _cacheService.SetAsync("test_key", testResponse, TimeSpan.FromMinutes(10));

        // Act - Get data
        var retrieved = await _cacheService.GetAsync<CachedResponse>("test_key");

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.StatusCode, Is.EqualTo(200));
        Assert.That(System.Text.Encoding.UTF8.GetString(retrieved.Body), Is.EqualTo("Test content"));

        // Act - Get all entries
        var entries = await _cacheService.GetAllEntriesAsync();

        Console.WriteLine($"Found {entries.Count} entries");
        foreach (var entry in entries)
        {
            Console.WriteLine($"Key: {entry.Key}, Type: {entry.Type}, Data: {entry.Data}");
        }

        // Assert
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Key, Is.EqualTo("test_key"));
    }
}
