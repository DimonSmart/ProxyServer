using DimonSmart.ProxyServer.Interfaces;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DimonSmart.ProxyServer.Services;

public class SqliteDiskCacheService : IDiskCacheService, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDiskCacheService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed = false;

    public SqliteDiskCacheService(string dbPath, ILogger<SqliteDiskCacheService> logger)
    {
        _logger = logger;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath};Cache=Shared;";
        InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeDatabaseAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS cache_entries (
                    key TEXT PRIMARY KEY,
                    type TEXT NOT NULL,
                    data BLOB NOT NULL,
                    expires_at INTEGER NOT NULL,
                    created_at INTEGER NOT NULL,
                    hit_count INTEGER DEFAULT 0,
                    size_bytes INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_expires_at ON cache_entries(expires_at);
                CREATE INDEX IF NOT EXISTS idx_created_at ON cache_entries(created_at);
            ";

            using var command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("SQLite disk cache initialized at {ConnectionString}", _connectionString);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_disposed) return null;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // First check if the item exists and is not expired
            var selectSql = @"
                SELECT data, type, expires_at 
                FROM cache_entries 
                WHERE key = @key AND expires_at > @now";

            using var selectCommand = new SqliteCommand(selectSql, connection);
            selectCommand.Parameters.AddWithValue("@key", key);
            selectCommand.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            using var reader = await selectCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var data = (byte[])reader["data"];
            var type = (string)reader["type"];

            // Update hit count
            var updateSql = "UPDATE cache_entries SET hit_count = hit_count + 1 WHERE key = @key";
            using var updateCommand = new SqliteCommand(updateSql, connection);
            updateCommand.Parameters.AddWithValue("@key", key);
            await updateCommand.ExecuteNonQueryAsync();

            // Deserialize based on type
            if (type == typeof(T).FullName)
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<T>(json);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache entry for key {Key}", key);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(value);
            var data = System.Text.Encoding.UTF8.GetBytes(json);
            var expiresAt = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
            var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var insertSql = @"
                INSERT OR REPLACE INTO cache_entries 
                (key, type, data, expires_at, created_at, hit_count, size_bytes)
                VALUES (@key, @type, @data, @expires_at, @created_at, 0, @size_bytes)";

            using var command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@type", typeof(T).FullName);
            command.Parameters.AddWithValue("@data", data);
            command.Parameters.AddWithValue("@expires_at", expiresAt);
            command.Parameters.AddWithValue("@created_at", createdAt);
            command.Parameters.AddWithValue("@size_bytes", data.Length);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache entry for key {Key}", key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        if (_disposed) return false;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT 1 FROM cache_entries WHERE key = @key AND expires_at > @now LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var result = await command.ExecuteScalarAsync();
            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if cache contains key {Key}", key);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM cache_entries WHERE key = @key";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@key", key);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entry for key {Key}", key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CleanupExpiredAsync()
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM cache_entries WHERE expires_at <= @now";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var deletedCount = await command.ExecuteNonQueryAsync();
            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired cache entries", deletedCount);
            }

            // Vacuum to reclaim space after cleanup
            var vacuumCommand = new SqliteCommand("VACUUM", connection);
            await vacuumCommand.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<long> GetSizeAsync()
    {
        if (_disposed) return 0;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COALESCE(SUM(size_bytes), 0) FROM cache_entries WHERE expires_at > @now";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache size");
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<int> GetCountAsync()
    {
        if (_disposed) return 0;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(*) FROM cache_entries WHERE expires_at > @now";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache count");
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM cache_entries";
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            // Vacuum to reclaim space
            var vacuumCommand = new SqliteCommand("VACUUM", connection);
            await vacuumCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _semaphore?.Dispose();
            _disposed = true;
        }
    }
}
