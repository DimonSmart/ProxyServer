using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using ProxyServer.FunctionalTests.TestServer;
using ProxyServer.FunctionalTests.Models;
using Xunit;
using Xunit.Abstractions;
using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace ProxyServer.FunctionalTests;

/// <summary>
/// Functional tests for the proxy server that validate caching, proxying, and authorization functionality
/// </summary>
public class ProxyServerFunctionalTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _testServer;
    private readonly IHost _proxyServer;
    private readonly HttpClient _httpClient;
    
    private const int TestServerPort = 11411;
    private const int ProxyServerPort = 15000;
    private const string TestPassword = "testpass123";

    /// <summary>
    /// Initializes the test environment with test server and proxy server
    /// </summary>
    /// <param name="output">Test output helper for logging</param>
    public ProxyServerFunctionalTests(ITestOutputHelper output)
    {
        _output = output;
        _httpClient = new HttpClient();
        
        // Start test server
        _testServer = TestServerHostBuilder.CreateTestServer(TestServerPort);
        _testServer.StartAsync().Wait();
        _output.WriteLine($"Test server started on port {TestServerPort}");

        // Start proxy server
        _proxyServer = CreateProxyServer();
        _proxyServer.StartAsync().Wait();
        _output.WriteLine($"Proxy server started on port {ProxyServerPort}");
        
        // Small delay for complete server initialization
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Tests that the proxy server forwards requests to upstream and caches responses properly
    /// </summary>
    [Fact]
    public async Task ProxyServer_ShouldForwardRequestToUpstream_AndCacheResponse()
    {
        // Arrange
        var request = new { Text = "Hello World" };
        var requestContent = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Reset test server statistics
        await ResetTestServerStats();

        // Act - First request through proxy
        var response1 = await MakeProxyRequest("/api/StringReverse/reverse", requestContent);
        
        // Add debug information
        if (response1.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response1.Content.ReadAsStringAsync();
            _output.WriteLine($"First request failed with status: {response1.StatusCode}, content: {errorContent}");
        }
        
        // Assert - Check first response
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var responseContent1 = await response1.Content.ReadAsStringAsync();
        _output.WriteLine($"First response: {responseContent1}");
        
        var responseData1 = JsonSerializer.Deserialize<ReverseResponse>(responseContent1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(responseData1);
        Assert.Equal("Hello World", responseData1.OriginalText);
        Assert.Equal("dlroW olleH", responseData1.ReversedText);
        Assert.Equal(1, responseData1.CallNumber);

        // Verify that test server was called once
        var stats1 = await GetTestServerStats();
        Assert.Equal(1, stats1.CallCount);

        // Act - Second request through proxy (should return from cache)
        var requestContent2 = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
            
        var response2 = await MakeProxyRequest("/api/StringReverse/reverse", requestContent2);

        // Assert - Check second response
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var responseContent2 = await response2.Content.ReadAsStringAsync();
        _output.WriteLine($"Second response: {responseContent2}");

        var responseData2 = JsonSerializer.Deserialize<ReverseResponse>(responseContent2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(responseData2);
        Assert.Equal("Hello World", responseData2.OriginalText);
        Assert.Equal("dlroW olleH", responseData2.ReversedText);
        
        // Important: CallNumber should remain 1, as response came from cache
        Assert.Equal(1, responseData2.CallNumber);

        // Verify that test server was NOT called again
        var stats2 = await GetTestServerStats();
        Assert.Equal(1, stats2.CallCount);
        
        _output.WriteLine("✓ Cache is working correctly - second request was served from cache");
    }

    /// <summary>
    /// Tests that different requests are cached separately
    /// </summary>
    [Fact]
    public async Task ProxyServer_ShouldHandleDifferentRequests_AndCacheThemSeparately()
    {
        // Arrange
        await ResetTestServerStats();

        var request1 = new { Text = "First" };
        var request2 = new { Text = "Second" };

        // Act & Assert - First request
        var response1 = await MakeProxyRequest("/api/StringReverse/reverse", 
            CreateJsonContent(request1));
        
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var data1 = await DeserializeResponse<ReverseResponse>(response1);
        Assert.Equal("tsriF", data1.ReversedText);
        Assert.Equal(1, data1.CallNumber);

        // Act & Assert - Second request (different data)
        var response2 = await MakeProxyRequest("/api/StringReverse/reverse", 
            CreateJsonContent(request2));
        
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var data2 = await DeserializeResponse<ReverseResponse>(response2);
        Assert.Equal("dnoceS", data2.ReversedText);
        Assert.Equal(2, data2.CallNumber);

        // Act & Assert - Repeat first request (from cache)
        var response3 = await MakeProxyRequest("/api/StringReverse/reverse", 
            CreateJsonContent(request1));
        
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        var data3 = await DeserializeResponse<ReverseResponse>(response3);
        Assert.Equal("tsriF", data3.ReversedText);
        Assert.Equal(1, data3.CallNumber); // From cache

        // Check overall statistics
        var stats = await GetTestServerStats();
        Assert.Equal(2, stats.CallCount); // Only 2 real calls

        _output.WriteLine("✓ Different requests are cached separately");
    }

    /// <summary>
    /// Tests that caching provides performance benefits and detailed cache information
    /// </summary>
    [Fact]
    public async Task ProxyServer_ShouldProvideDetailedCacheInfo()
    {
        // Arrange
        await ResetTestServerStats();

        var request = new { Text = "Cache Test" };

        // Act & Assert - First request (not from cache)
        var start1 = DateTime.UtcNow;
        var response1 = await MakeProxyRequest("/api/StringReverse/reverse", 
            CreateJsonContent(request));
        var end1 = DateTime.UtcNow;
        var duration1 = end1 - start1;
        
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var data1 = await DeserializeResponse<ReverseResponse>(response1);
        Assert.Equal("tseT ehcaC", data1.ReversedText);
        Assert.Equal(1, data1.CallNumber);

        _output.WriteLine($"First request took: {duration1.TotalMilliseconds}ms");

        // Act & Assert - Second request (from cache, should be faster)
        var start2 = DateTime.UtcNow;
        var response2 = await MakeProxyRequest("/api/StringReverse/reverse", 
            CreateJsonContent(request));
        var end2 = DateTime.UtcNow;
        var duration2 = end2 - start2;
        
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var data2 = await DeserializeResponse<ReverseResponse>(response2);
        Assert.Equal("tseT ehcaC", data2.ReversedText);
        Assert.Equal(1, data2.CallNumber); // From cache, same number

        _output.WriteLine($"Second request (cached) took: {duration2.TotalMilliseconds}ms");

        // Cached request should be significantly faster
        Assert.True(duration2 < duration1, 
            $"Cached request should be faster. First: {duration1.TotalMilliseconds}ms, Second: {duration2.TotalMilliseconds}ms");

        // Verify that test server was called only once
        var stats = await GetTestServerStats();
        Assert.Equal(1, stats.CallCount);

        _output.WriteLine("✓ Cache performance verified - cached responses are significantly faster");
    }

    /// <summary>
    /// Creates and configures a proxy server instance for testing
    /// </summary>
    /// <returns>A configured proxy server host</returns>
    private IHost CreateProxyServer()
    {
        var settings = new ProxySettings
        {
            UpstreamUrl = $"http://localhost:{TestServerPort}",
            UseMemoryCache = true,
            CacheDurationSeconds = 300, // 5 minutes for tests
            CacheMaxEntries = 100,
            Port = ProxyServerPort,
            AllowedCredentials = new List<DimonSmart.ProxyServer.Models.CredentialPair>
            {
                new()
                {
                    IPs = new List<string> { "127.0.0.1", "::1", "*.*.*.*" },
                    Passwords = new List<string> { TestPassword }
                }
            }
        };

        var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseUrls($"http://localhost:{ProxyServerPort}")
                    .ConfigureKestrel(options =>
                    {
                        options.AllowSynchronousIO = true; // Allow synchronous operations for tests
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddProxyServices(settings);
                    })
                    .Configure(app =>
                    {
                        app.UseProxyServer();
                    });
            });

        return builder.Build();
    }

    /// <summary>
    /// Makes an authenticated request to the proxy server
    /// </summary>
    /// <param name="path">The API path to request</param>
    /// <param name="content">The HTTP content to send</param>
    /// <returns>The HTTP response from the proxy server</returns>
    private async Task<HttpResponseMessage> MakeProxyRequest(string path, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{ProxyServerPort}{path}")
        {
            Content = content
        };
        
        // Add Basic Auth header
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"user:{TestPassword}"));
        request.Headers.Add("Authorization", $"Basic {credentials}");
        
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// Creates JSON content from an object for HTTP requests
    /// </summary>
    /// <param name="data">The object to serialize to JSON</param>
    /// <returns>StringContent with JSON data</returns>
    private StringContent CreateJsonContent(object data)
    {
        return new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json");
    }

    /// <summary>
    /// Deserializes HTTP response content to the specified type
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="response">The HTTP response containing JSON data</param>
    /// <returns>The deserialized object</returns>
    private async Task<T> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    /// <summary>
    /// Gets current statistics from the test server
    /// </summary>
    /// <returns>Test server statistics</returns>
    private async Task<TestServerStats> GetTestServerStats()
    {
        var response = await _httpClient.GetAsync($"http://localhost:{TestServerPort}/api/StringReverse/stats");
        return await DeserializeResponse<TestServerStats>(response);
    }

    /// <summary>
    /// Resets the test server statistics
    /// </summary>
    private async Task ResetTestServerStats()
    {
        await _httpClient.PostAsync($"http://localhost:{TestServerPort}/api/StringReverse/reset", null);
    }

    /// <summary>
    /// Disposes of all resources used by the test
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
        _testServer?.StopAsync().Wait();
        _testServer?.Dispose();
        _proxyServer?.StopAsync().Wait();
        _proxyServer?.Dispose();
    }
}
