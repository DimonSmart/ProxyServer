using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Models;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Text.Json;

namespace ProxyServer.FunctionalTests;

[TestFixture]
public class CacheDumpTests
{
    private DatabaseCacheService _cacheService = null!;
    private ProxySettings _settings = null!;
    private ILogger<DatabaseCacheService> _logger = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid():N}.db");
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
    public async Task GetAllEntriesAsync_EmptyCache_ReturnsEmptyList()
    {
        // Act
        var entries = await _cacheService.GetAllEntriesAsync();

        // Assert
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Is.Empty);
    }

    [Test]
    public async Task GetAllEntriesAsync_WithData_ReturnsEntries()
    {
        // Arrange
        var testResponse1 = new CachedResponse(
            StatusCode: 200,
            Headers: new Dictionary<string, string[]> { { "Content-Type", new[] { "text/plain" } } },
            Body: System.Text.Encoding.UTF8.GetBytes("Test content 1")
        );

        var testResponse2 = new CachedResponse(
            StatusCode: 200,
            Headers: new Dictionary<string, string[]> { { "Content-Type", new[] { "application/json" } } },
            Body: System.Text.Encoding.UTF8.GetBytes("Test content 2 with Liberté, égalité, fraternité")
        );

        await _cacheService.SetAsync("test_key_1", testResponse1, TimeSpan.FromMinutes(10));
        await _cacheService.SetAsync("test_key_2", testResponse2, TimeSpan.FromMinutes(10));

        // Act
        var entries = await _cacheService.GetAllEntriesAsync();

        // Assert
        Assert.That(entries, Has.Count.EqualTo(2));

        var entry1 = entries.FirstOrDefault(e => e.Key == "test_key_1");
        Assert.That(entry1, Is.Not.Null);
        Assert.That(entry1!.Type, Is.EqualTo(typeof(CachedResponse).FullName));

        var entry2 = entries.FirstOrDefault(e => e.Key == "test_key_2");
        Assert.That(entry2, Is.Not.Null);

        // The Body is base64 encoded in the JSON, so we check for the base64 representation of "Test content 2 with Liberté, égalité, fraternité"
        Assert.That(entry2!.Data, Does.Contain("VGVzdCBjb250ZW50IDIgd2l0aCBMaWJlcnTDqSwgw6lnYWxpdMOpLCBmcmF0ZXJuaXTDqQ=="));
    }

    [Test]
    public async Task GetAllEntriesAsync_WithFilter_ReturnsFilteredEntries()
    {
        // Arrange
        var testResponse = new CachedResponse(
            StatusCode: 200,
            Headers: new Dictionary<string, string[]>(),
            Body: System.Text.Encoding.UTF8.GetBytes("Test content")
        );

        await _cacheService.SetAsync("user_123_profile", testResponse, TimeSpan.FromMinutes(10));
        await _cacheService.SetAsync("user_456_settings", testResponse, TimeSpan.FromMinutes(10));
        await _cacheService.SetAsync("admin_data", testResponse, TimeSpan.FromMinutes(10));

        // Act
        var allEntries = await _cacheService.GetAllEntriesAsync();
        var filteredEntries = await _cacheService.GetAllEntriesAsync("user_");

        // Assert
        Assert.That(allEntries, Has.Count.EqualTo(3));
        Assert.That(filteredEntries, Has.Count.EqualTo(2));
        Assert.That(filteredEntries.All(e => e.Key.Contains("user_")), Is.True);
    }

    [Test]
    public void CacheDump_SerializesToJson_WithUnicodeCharacters()
    {
        // Arrange
        var dump = new CacheDump
        {
            TotalEntries = 1,
            FilteredEntries = 1,
            Filter = "test",
            Entries = new List<CacheEntry>
            {
                new CacheEntry
                {
                    Key = "test_français_key",
                    Type = "TestType",
                    Data = "{\"message\": \"Bonjour le monde!\"}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                }
            }
        };

        // Act
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(dump, jsonOptions);

        // Assert
        Assert.That(json, Does.Contain("test_français_key"));
        Assert.That(json, Does.Contain("Bonjour le monde!"));
        Assert.That(json, Does.Not.Contain("\\u"));  // Should not be escaped
    }
}
