# ProxyServer

[![Functional Tests](https://github.com/DimonSmart/ProxyServer/actions/workflows/functional-tests.yml/badge.svg?branch=main)](https://github.com/DimonSmart/ProxyServer/actions/workflows/functional-tests.yml)

Caching proxy server with authentication and access control.

## Features

### ✨ Core Capabilities

- **Proxy Server**: Forwards HTTP requests to upstream servers
- **Caching**: Memory and disk-based response caching for improved performance
- **Authentication**: Basic Auth with password support
- **Access Control**: IP address filtering with mask support
- **Security**: HTTPS support with certificates
- **Command Line Interface**: Management commands for cache operations

### 🔧 Configuration

Configuration is done through the `settings.json` file:

```json
{
  "AllowedCredentials": [
    {
      "IPs": [ "127.0.0.1", "::1", "192.168.*.*" ],
      "Passwords": [ "testpass", "anotherpass" ]
    }
  ],
  "UpstreamUrl": "http://localhost:11434",
  "EnableMemoryCache": true,
  "EnableDiskCache": true,
  "Port": 8042,
  "MemoryCache": {
    "TtlSeconds": 1800,
    "MaxEntries": 10000
  },
  "DiskCache": {
    "CachePath": "./cache/proxy_cache.db",
    "TtlSeconds": 604800,
    "MaxSizeMB": 1024,
    "CleanupIntervalMinutes": 60
  },
  "Port": 8042,
  "CertificatePath": "certificate.pfx",
  "CertificatePassword": "password"
}
```

### 🖥️ Command Line Interface

The proxy server supports command-line operations for management:

```bash
# Show help
dotnet run help

# Show current configuration
dotnet run config

# Clear all cached data
dotnet run clear-cache
```

#### Available Commands

- **`help`**: Display help information and available commands
- **`config`**: Show current configuration including cache settings, ports, and file locations
- **`clear-cache`**: Clear all cached data from both memory and disk caches

## CI/CD

### GitHub Actions

The project includes a configured GitHub Actions workflow for automatic test execution:

- **Triggers**: push to main/develop, pull request to main
- **Platform**: Windows Latest
- **Steps**: Build → Test → Upload Results

Configuration: `.github/workflows/functional-tests.yml`

### Local Execution

```bash
# Quick test run (PowerShell)
.\run-tests.ps1

# Detailed analysis
dotnet test --logger "console;verbosity=detailed"
```

## Technical Details

### HTTP Headers and Caching

The proxy server correctly handles different response types:

**Streaming Responses**: When the upstream server sends chunked or streaming responses, the proxy forwards them in real-time while preserving the original headers (`Transfer-Encoding: chunked`, etc.).

**Cached Responses**: When serving responses from cache, transport-level headers like `Transfer-Encoding` and `Content-Length` are filtered out to prevent HTTP protocol conflicts. This is because:
- Cached responses are stored as complete byte arrays
- Sending `Transfer-Encoding: chunked` headers with non-chunked data causes client hangs
- The proxy automatically sets appropriate headers for cached content

**Filtered Headers**: The following headers are removed from cached responses to ensure compatibility:
- `transfer-encoding`, `content-length`, `connection`
- `date`, `server`, `via`, `warning`, `age`, `etag`
- `expires`, `last-modified`, `cache-control`, `vary`

## Monitoring

### Test Server Metrics

The test API provides monitoring endpoints:

- `GET /api/StringReverse/stats` - call statistics
- `POST /api/StringReverse/reset` - reset counters

