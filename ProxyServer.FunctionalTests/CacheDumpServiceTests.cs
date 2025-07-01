using DimonSmart.ProxyServer.Models;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Text.Json;

namespace ProxyServer.FunctionalTests;

[TestFixture]
public class CacheDumpServiceTests
{
    private CacheDumpService _dumpService = null!;
    private ILogger<CacheDumpService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CacheDumpService>();
        _dumpService = new CacheDumpService(_logger);
    }

    [Test]
    public void CreateBasicDump_WithEmptyEntries_ReturnsValidDump()
    {
        // Arrange
        var entries = new List<CacheEntry>();

        // Act
        var dump = _dumpService.CreateBasicDump(entries);

        // Assert
        Assert.That(dump, Is.Not.Null);
        Assert.That(dump.TotalEntries, Is.EqualTo(0));
        Assert.That(dump.FilteredEntries, Is.EqualTo(0));
        Assert.That(dump.Entries, Is.Empty);
        Assert.That(dump.Filter, Is.Null);
    }

    [Test]
    public void CreateBasicDump_WithEntries_ReturnsValidDump()
    {
        // Arrange
        var entries = new List<CacheEntry>
        {
            new CacheEntry
            {
                Key = "test_key_1",
                Type = "DimonSmart.ProxyServer.Models.CachedResponse",
                Data = "base64_data_1",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            },
            new CacheEntry
            {
                Key = "test_key_2",
                Type = "DimonSmart.ProxyServer.Models.CachedResponse",
                Data = "base64_data_2",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            }
        };

        // Act
        var dump = _dumpService.CreateBasicDump(entries, "test_filter");

        // Assert
        Assert.That(dump.TotalEntries, Is.EqualTo(2));
        Assert.That(dump.FilteredEntries, Is.EqualTo(2));
        Assert.That(dump.Filter, Is.EqualTo("test_filter"));
        Assert.That(dump.Entries, Has.Count.EqualTo(2));
    }

    [Test]
    public void CreateDetailedDump_WithValidCachedResponse_ReturnsDetailedDump()
    {
        // Arrange
        var cachedResponse = new CachedResponse(
            StatusCode: 200,
            Headers: new Dictionary<string, string[]> { { "Content-Type", new[] { "application/json" } } },
            Body: System.Text.Encoding.UTF8.GetBytes("{\"message\": \"Hello World\"}")
        );

        var cachedResponseJson = JsonSerializer.Serialize(cachedResponse);

        var entries = new List<CacheEntry>
        {
            new CacheEntry
            {
                Key = "test_cache_key",
                Type = "DimonSmart.ProxyServer.Models.CachedResponse",
                Data = cachedResponseJson,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            }
        };

        // Act
        var dump = _dumpService.CreateDetailedDump(entries);

        // Assert
        Assert.That(dump, Is.Not.Null);
        Assert.That(dump.TotalEntries, Is.EqualTo(1));
        Assert.That(dump.FilteredEntries, Is.EqualTo(1));
        Assert.That(dump.Entries, Has.Count.EqualTo(1));

        var detailedEntry = dump.Entries.First();
        Assert.That(detailedEntry.CacheKeyHash, Is.EqualTo("test_cache_key"));
        Assert.That(detailedEntry.Response.StatusCode, Is.EqualTo(200));
        Assert.That(detailedEntry.Response.ContentType, Is.EqualTo("application/json"));
        Assert.That(detailedEntry.Response.Body, Is.EqualTo("{\"message\": \"Hello World\"}"));
        Assert.That(detailedEntry.Response.BodySizeBytes, Is.EqualTo(26)); // Length of the JSON string in bytes

        // Verify statistics are populated
        Assert.That(dump.Statistics.TotalSizeBytes, Is.EqualTo(26));
        Assert.That(dump.Statistics.StatusCodes[200], Is.EqualTo(1));
        Assert.That(dump.Statistics.ContentTypes["application/json"], Is.EqualTo(1));
    }

    [Test]
    public void CreateDetailedDump_WithNonHttpCacheEntry_ReturnsBasicDetailedEntry()
    {
        // Arrange
        var entries = new List<CacheEntry>
        {
            new CacheEntry
            {
                Key = "non_http_key",
                Type = "System.String",
                Data = "simple_string_data",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            }
        };

        // Act
        var dump = _dumpService.CreateDetailedDump(entries);

        // Assert
        Assert.That(dump.Entries, Has.Count.EqualTo(1));
        var entry = dump.Entries.First();
        Assert.That(entry.CacheKeyHash, Is.EqualTo("non_http_key"));
        Assert.That(entry.CacheKey, Is.EqualTo("Unknown (non-HTTP cache entry)"));
    }

    [Test]
    public void CreateDetailedDump_WithInvalidJsonData_SkipsEntry()
    {
        // Arrange
        var entries = new List<CacheEntry>
        {
            new CacheEntry
            {
                Key = "invalid_json_key",
                Type = "DimonSmart.ProxyServer.Models.CachedResponse",
                Data = "invalid_json_data",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            }
        };

        // Act
        var dump = _dumpService.CreateDetailedDump(entries);

        // Assert
        // The service should skip entries with invalid JSON and continue processing
        Assert.That(dump.TotalEntries, Is.EqualTo(1));
        Assert.That(dump.FilteredEntries, Is.EqualTo(1));
        Assert.That(dump.Entries, Is.Empty); // Invalid entry should be skipped
    }
}
