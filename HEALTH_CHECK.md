# Health Check and Statistics Endpoints

Health check endpoints have been added to ProxyServer for monitoring status and statistics.

## Available Endpoints

### 1. Health Check - `/health`
Returns complete information about the proxy server status:

```json
{
  "status": "Healthy",
  "version": "1.0.0.0",
  "uptime": "00:05:23.1234567",
  "cache": {
    "totalRequests": 45,
    "cacheHits": 32,
    "cacheMisses": 13,
    "hitRate": 71.11,
    "currentEntries": 12,
    "maxEntries": 1000,
    "usagePercentage": 1.2,
    "isEnabled": true
  },
  "upstreamUrl": "http://localhost:11434",
  "timestamp": "2025-06-25T11:29:23.2415568Z",
  "details": {
    "port": 5000,
    "cachingEnabled": true,
    "cacheDurationSeconds": 60,
    "authenticationEnabled": true
  }
}
```

### 2. Ping - `/ping`
Simple endpoint for quick availability check:

```json
{
  "status": "ok",
  "timestamp": "2025-06-25T11:29:49.9755191Z"
}
```

### 3. Cache Statistics - `/stats/cache`
Detailed cache statistics:

```json
{
  "totalRequests": 45,
  "cacheHits": 32,
  "cacheMisses": 13,
  "hitRate": 71.11,
  "currentEntries": 12,
  "maxEntries": 1000,
  "usagePercentage": 1.2,
  "isEnabled": true
}
```

## Features

- **No authentication**: All health check endpoints are accessible without Basic Auth for monitoring convenience
- **Automatic statistics**: Cache automatically tracks hits/misses
- **Real-time data**: All statistics are updated in real time
- **Security**: Endpoints do not expose sensitive information

## Usage

### System monitoring
```bash
# Check status
curl http://localhost:5000/health

# Quick availability check
curl http://localhost:5000/ping

# Cache statistics
curl http://localhost:5000/stats/cache
```

