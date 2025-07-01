using DimonSmart.ProxyServer;
using DimonSmart.ProxyServer.Models;
using DimonSmart.ProxyServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace ProxyServer.FunctionalTests;

public class EndpointCacheTests
{
    [Fact]
    public void PatternMatcher_Should_Match_Exact_Path()
    {
        // Arrange
        var pattern = "/api/models";
        var path = "/api/models";

        // Act
        var result = DimonSmart.ProxyServer.Utilities.PatternMatcher.IsMatch(pattern, path);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PatternMatcher_Should_Match_Wildcard_Pattern()
    {
        // Arrange
        var pattern = "/api/models/*";
        var path = "/api/models/list";

        // Act
        var result = DimonSmart.ProxyServer.Utilities.PatternMatcher.IsMatch(pattern, path);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PatternMatcher_Should_Not_Match_Different_Path()
    {
        // Arrange
        var pattern = "/api/models";
        var path = "/api/tags";

        // Act
        var result = DimonSmart.ProxyServer.Utilities.PatternMatcher.IsMatch(pattern, path);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CachePolicyService_Should_Return_Custom_TTL_For_Matching_Rule()
    {
        // Arrange
        var proxySettings = new ProxySettings
        {
            EnableMemoryCache = true,
            EndpointCacheRules = new List<EndpointCacheRule>
            {
                new()
                {
                    PathPattern = "/api/models",
                    Methods = new List<string> { "GET" },
                    TtlSeconds = 300,
                    Enabled = true
                }
            }
        };

        var logger = new TestLogger<CachePolicyService>();
        var cachePolicyService = new CachePolicyService(
            Options.Create(proxySettings),
            logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/models";

        // Act
        var ttl = cachePolicyService.GetCacheTtl(context);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(300), ttl);
    }

    [Fact]
    public void CachePolicyService_Should_Return_Zero_For_No_Matching_Rule()
    {
        // Arrange
        var proxySettings = new ProxySettings
        {
            EnableMemoryCache = true,
            EndpointCacheRules = new List<EndpointCacheRule>
            {
                new()
                {
                    PathPattern = "/api/models",
                    Methods = new List<string> { "GET" },
                    TtlSeconds = 300,
                    Enabled = true
                }
            }
        };

        var logger = new TestLogger<CachePolicyService>();
        var cachePolicyService = new CachePolicyService(
            Options.Create(proxySettings),
            logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/different";

        // Act
        var ttl = cachePolicyService.GetCacheTtl(context);

        // Assert
        Assert.Equal(TimeSpan.Zero, ttl);
    }

    [Fact]
    public void CachePolicyService_Should_Return_Zero_When_No_Rules_Match()
    {
        // Arrange
        var proxySettings = new ProxySettings
        {
            EnableMemoryCache = true,
            EndpointCacheRules = new List<EndpointCacheRule>()
        };

        var logger = new TestLogger<CachePolicyService>();
        var cachePolicyService = new CachePolicyService(
            Options.Create(proxySettings),
            logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/any";

        // Act
        var ttl = cachePolicyService.GetCacheTtl(context);

        // Assert
        Assert.Equal(TimeSpan.Zero, ttl);
    }
}
