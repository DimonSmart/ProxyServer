using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ProxyServer.FunctionalTests;

[TestFixture]
public class ReproduceBugTest
{
    private DatabaseCacheService _cacheService = null!;
    private ProxySettings _settings = null!;
    private ILogger<DatabaseCacheService> _logger = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"repro_cache_{Guid.NewGuid():N}.db");
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
}
