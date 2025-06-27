# Copilot Instructions for ProxyServer Project

## Project Overview

This is a **Caching Proxy Server** written in C# using ASP.NET Core (.NET 8). The project implements a high-performance HTTP proxy with built-in caching capabilities, access control, and authentication. Primary use case is exposing local Ollama servers for remote access and providing response caching for faster debugging during Ollama-related development.

## Core Commands

### Build & Run
- `dotnet build` - Build solution
- `dotnet run` - Start proxy server (default port 8042)
- `dotnet run --project ProxyServer` - Run from solution root

### Testing
- `.\run-tests.ps1` - Quick test run (PowerShell script)
- `dotnet test` - Run all tests
- `dotnet test ProxyServer.FunctionalTests` - Run functional tests only
- `dotnet test --logger "console;verbosity=detailed"` - Detailed test output

### Management Commands
- `dotnet run help` - Show available commands
- `dotnet run config` - Display current configuration
- `dotnet run clear-cache` - Clear all cached data

### CI/CD
- GitHub Actions workflow: `.github/workflows/functional-tests.yml`
- Runs on: Windows Latest, triggers on push to main/develop, PRs to main

## Architecture

### Project Structure
- **ProxyServer** (`DimonSmart.ProxyServer.csproj`) - Main application
- **ProxyServer.FunctionalTests** - Integration/functional tests using ASP.NET Core TestHost

### Key Dependencies
- ASP.NET Core (.NET 8) - Web framework
- Microsoft.Data.Sqlite - SQLite database for disk caching
- Microsoft.Extensions.Caching.Memory - In-memory caching
- NUnit + xUnit - Testing frameworks

### Core Services (Dependency Injection)
- `IProxyService` - HTTP request forwarding with timeout support
- `ICacheService` - Cache abstraction with composable implementation
- `IExtendedCacheService` - Extended cache with cleanup/monitoring
- `IAccessControlService` - IP filtering and Basic Auth
- `ICachePolicyService` - Cache behavior policies  
- `ICacheKeyService` - Cache key generation
- `IResponseWriterService` - HTTP response writing with header filtering
- `IExceptionHandlingService` - Global exception handling

### Cache Architecture
Composable cache system using decorator pattern:
- **Memory only**: `MemoryCacheService`
- **Disk only**: `DatabaseCacheService` (SQLite)
- **Memory + Disk**: `ComposableCacheService` (memory as L1, disk as L2)
- **No cache**: `NullCacheService`

### Data Stores
- **Memory Cache**: Microsoft.Extensions.Caching.Memory with configurable size/TTL
- **Disk Cache**: SQLite database at `./cache/proxy_cache.db`
- **Configuration**: JSON file `settings.json`

### Middleware Pipeline
1. `ExceptionHandlingMiddleware` - Global error handling
2. `AccessControlMiddleware` - IP filtering and authentication
3. `ProxyMiddleware` - Core proxy logic with caching

## Configuration

### Settings File: `settings.json`
```json
{
  "AllowedCredentials": [{ "IPs": ["127.0.0.1", "192.168.*.*"], "Passwords": ["pass"] }],
  "UpstreamUrl": "http://localhost:11434",
  "Port": 8042,
  "UpstreamTimeoutSeconds": 1800,
  "EnableMemoryCache": true,
  "EnableDiskCache": true,
  "MemoryCache": { "TtlSeconds": 1800, "MaxEntries": 10000 },
  "DiskCache": { "CachePath": "./cache/proxy_cache.db", "TtlSeconds": 604800, "MaxSizeMB": 1024 },
  "StreamingCache": { "EnableStreamingCache": true, "ChunkSize": 256, "ChunkDelayMs": 10 }
}
```

## Coding Standards

### Language & Style
- **Language**: All code, comments, and documentation in **English**
- **Framework**: ASP.NET Core patterns and conventions
- **Nullable**: Nullable reference types enabled (`<Nullable>enable</Nullable>`)

### File Organization
- One class per file following C# conventions
- Organize by feature: Controllers/, Services/, Interfaces/, Middleware/, Models/
- Use namespace `DimonSmart.ProxyServer.*` 

### Naming Conventions
- **Classes/Methods/Properties**: PascalCase
- **Local variables/parameters**: camelCase
- **Interfaces**: IPascalCase prefix
- **Private fields**: _camelCase with underscore

### Documentation
- Document only complex business logic and caching behavior
- Skip obvious comments

### Error Handling
- Use `IExceptionHandlingService` for global exception handling
- Structured logging with Microsoft.Extensions.Logging
- Return appropriate HTTP status codes
- Handle upstream timeouts gracefully

### Testing Requirements
- Functional tests for all major features
- Use ASP.NET Core TestHost for integration testing
- Test cache hit/miss scenarios
- Verify header filtering for cached vs streaming responses
- Mock upstream servers for reliable testing

### HTTP Header Handling
**Critical**: When serving cached responses, filter out transport-level headers to prevent HTTP protocol conflicts:
- Remove: `transfer-encoding`, `content-length`, `connection`
- Remove cache-related: `date`, `etag`, `expires`, `cache-control`
- Preserve streaming headers only for real-time responses

### Performance Considerations
- Use composable cache pattern for optimal performance
- Configure appropriate cache TTL values
- Monitor cache hit rates via logging
- Handle streaming responses without buffering
- Use background services for cache cleanup

