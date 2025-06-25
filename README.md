# ProxyServer

Caching proxy server with authentication and access control.

## Features

### ✨ Core Capabilities

- **Proxy Server**: Forwards HTTP requests to upstream servers
- **Caching**: In-memory response caching for improved performance
- **Authentication**: Basic Auth with password support
- **Access Control**: IP address filtering with mask support
- **Security**: HTTPS support with certificates

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
  "UseMemoryCache": true,
  "CacheDurationSeconds": 60,
  "CacheMaxEntries": 1000,
  "Port": 5000,
  "CertificatePath": "certificate.pfx",
  "CertificatePassword": "password"
}
```

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

## Monitoring

### Test Server Metrics

The test API provides monitoring endpoints:

- `GET /api/StringReverse/stats` - call statistics
- `POST /api/StringReverse/reset` - reset counters

