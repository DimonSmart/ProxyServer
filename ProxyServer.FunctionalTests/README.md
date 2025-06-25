# Proxy Server Functional Tests

This project contains functional tests for the DimonSmart.ProxyServer proxy server.

## Test Description

### ProxyServerFunctionalTests

The class contains the following tests:

1. **ProxyServer_ShouldForwardRequestToUpstream_AndCacheResponse** - Main caching test:
   - Starts a test server that reverses strings
   - Configures the proxy server to forward requests to the test server
   - Makes the first request and verifies it reaches the test server
   - Makes a second identical request and verifies it was returned from cache (test server was NOT called again)

2. **ProxyServer_ShouldHandleDifferentRequests_AndCacheThemSeparately** - Separate caching test:
   - Verifies that different requests are cached separately
   - Identical requests are returned from cache
   - Different requests call the real service

3. **ProxyServer_ShouldProvideDetailedCacheInfo** - Cache performance test:
   - Verifies that cached requests execute significantly faster
   - Measures execution time of first (non-cached) and second (cached) requests
   - Confirms that cached responses are returned without calling the upstream server

### Test Server (StringReverseController)

A simple API server that:
- Accepts POST requests to `/api/StringReverse/reverse` with JSON `{ "Text": "string" }`
- Returns the reversed string along with call number and timestamp
- Provides endpoints for getting and resetting call statistics

## Configuration

Tests automatically:
- Start the test server on port 11411
- Start the proxy server on port 15000
- Configure authorization (Basic Auth with password "testpass123")
- Allow requests from localhost (127.0.0.1)
- Enable caching for 5 minutes
- Configure AllowSynchronousIO for compatibility with CacheService

## Running Tests

```bash
dotnet test
```
