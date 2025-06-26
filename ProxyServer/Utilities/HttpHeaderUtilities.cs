namespace DimonSmart.ProxyServer.Utilities;

/// <summary>
/// Utility class for HTTP header operations
/// </summary>
public static class HttpHeaderUtilities
{
    /// <summary>
    /// Headers that shouldn't be copied to maintain transparency and avoid conflicts
    /// </summary>
    private static readonly string[] RestrictedHeaders = 
    [
        "connection", "content-length", "transfer-encoding", "upgrade",
        "date", "server", "via", "warning", "age", "etag", "expires",
        "last-modified", "cache-control", "vary"
    ];

    /// <summary>
    /// Checks if a header should not be copied when proxying requests/responses
    /// </summary>
    /// <param name="headerName">The header name to check</param>
    /// <returns>True if the header should not be copied, false otherwise</returns>
    public static bool IsRestrictedHeader(string headerName)
    {
        return RestrictedHeaders.Contains(headerName.ToLowerInvariant());
    }
}
