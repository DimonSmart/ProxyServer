using DimonSmart.ProxyServer.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Authentication;

namespace DimonSmart.ProxyServer.Services;

public interface IConnectionDiagnosticsService
{
    Task<ConnectionDiagnostics> DiagnoseConnectionAsync(string url, CancellationToken cancellationToken = default);
    void LogRequestDiagnostics(HttpContext context, string targetUrl);
    void LogResponseDiagnostics(HttpResponseMessage response, TimeSpan elapsed);
    void LogSslException(Exception exception, string url);
}

public class ConnectionDiagnosticsService : IConnectionDiagnosticsService
{
    private readonly ILogger<ConnectionDiagnosticsService> _logger;
    private readonly ProxySettings _settings;

    public ConnectionDiagnosticsService(
        ILogger<ConnectionDiagnosticsService> logger,
        ProxySettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public async Task<ConnectionDiagnostics> DiagnoseConnectionAsync(string url, CancellationToken cancellationToken = default)
    {
        var diagnostics = new ConnectionDiagnostics { TargetUrl = url };
        var uri = new Uri(url);

        try
        {
            // DNS Resolution
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var hostEntry = await Dns.GetHostEntryAsync(uri.Host, cancellationToken);
            diagnostics.DnsResolutionTime = stopwatch.Elapsed;
            diagnostics.ResolvedIpAddresses = hostEntry.AddressList.Select(ip => ip.ToString()).ToList();
            
            _logger.LogDebug("DNS resolution for {Host} completed in {Time}ms. IPs: {IPs}", 
                uri.Host, diagnostics.DnsResolutionTime.TotalMilliseconds, string.Join(", ", diagnostics.ResolvedIpAddresses));

            // Ping Test
            if (diagnostics.ResolvedIpAddresses.Any())
            {
                var ping = new Ping();
                var firstIp = IPAddress.Parse(diagnostics.ResolvedIpAddresses.First());
                try
                {
                    var pingReply = await ping.SendPingAsync(firstIp, 5000);
                    diagnostics.PingSuccess = pingReply.Status == IPStatus.Success;
                    diagnostics.PingTime = pingReply.RoundtripTime;
                    
                    _logger.LogDebug("Ping to {IP}: {Status}, Time: {Time}ms", 
                        firstIp, pingReply.Status, pingReply.RoundtripTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Ping failed for {IP}: {Error}", firstIp, ex.Message);
                }
            }

            // Port connectivity test
            diagnostics.PortOpen = await TestPortConnectivityAsync(uri.Host, uri.Port, cancellationToken);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection diagnostics failed for {Url}", url);
            diagnostics.Error = ex.Message;
        }

        return diagnostics;
    }

    public void LogRequestDiagnostics(HttpContext context, string targetUrl)
    {
        var request = context.Request;
        var connection = context.Connection;

        _logger.LogInformation("=== REQUEST DIAGNOSTICS ===");
        _logger.LogInformation("Client: {ClientIP}:{ClientPort} -> Proxy: {LocalIP}:{LocalPort}", 
            connection.RemoteIpAddress, connection.RemotePort, 
            connection.LocalIpAddress, connection.LocalPort);
        _logger.LogInformation("Request: {Method} {Scheme}://{Host}{Path}{Query}", 
            request.Method, request.Scheme, request.Host, request.Path, request.QueryString);
        _logger.LogInformation("Target: {TargetUrl}", targetUrl);
        _logger.LogInformation("User-Agent: {UserAgent}", request.Headers.UserAgent.ToString() ?? "N/A");
        _logger.LogInformation("Content-Type: {ContentType}, Content-Length: {ContentLength}", 
            request.ContentType ?? "N/A", request.ContentLength?.ToString() ?? "N/A");

        // Log important headers
        var headersToLog = new[] { "Authorization", "Accept", "Accept-Encoding", "Connection", "Upgrade" };
        foreach (var headerName in headersToLog)
        {
            if (request.Headers.TryGetValue(headerName, out var headerValues) && headerValues.Count > 0)
            {
                _logger.LogInformation("Header {HeaderName}: {HeaderValue}", headerName, string.Join(", ", headerValues.Where(v => !string.IsNullOrEmpty(v))));
            }
        }

        // SSL/TLS info if HTTPS
        if (request.IsHttps)
        {
            _logger.LogInformation("TLS Protocol: {TlsProtocol}", connection.ClientCertificate?.NotBefore);
        }

        // Check scheme mismatch
        var targetUri = new Uri(targetUrl);
        if (request.Scheme != targetUri.Scheme)
        {
            _logger.LogWarning("⚠️  SCHEME MISMATCH: Client uses {ClientScheme} but upstream is {UpstreamScheme}", 
                request.Scheme, targetUri.Scheme);
        }
    }

    public void LogResponseDiagnostics(HttpResponseMessage response, TimeSpan elapsed)
    {
        _logger.LogInformation("=== RESPONSE DIAGNOSTICS ===");
        _logger.LogInformation("Status: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
        _logger.LogInformation("Response Time: {ElapsedMs}ms", elapsed.TotalMilliseconds);
        _logger.LogInformation("Content-Type: {ContentType}, Content-Length: {ContentLength}", 
            response.Content.Headers.ContentType, response.Content.Headers.ContentLength);

        // Log response headers
        var headersToLog = new[] { "Server", "Date", "Cache-Control", "Transfer-Encoding", "Connection" };
        foreach (var headerName in headersToLog)
        {
            if (response.Headers.TryGetValues(headerName, out var headerValues) && headerValues.Any())
            {
                _logger.LogInformation("Header {HeaderName}: {HeaderValue}", headerName, string.Join(", ", headerValues.Where(v => !string.IsNullOrEmpty(v))));
            }
        }

        // SSL/TLS info if available
        if (response.RequestMessage?.RequestUri?.Scheme == "https")
        {
            _logger.LogInformation("HTTPS connection established successfully");
        }
    }

    public void LogSslException(Exception exception, string url)
    {
        _logger.LogError("=== SSL/TLS ERROR DIAGNOSTICS ===");
        _logger.LogError("URL: {Url}", url);
        _logger.LogError("Exception Type: {ExceptionType}", exception.GetType().Name);
        _logger.LogError("Message: {Message}", exception.Message);

        if (exception is AuthenticationException authEx)
        {
            _logger.LogError("🔒 SSL/TLS Authentication failed");
            
            if (authEx.Message.Contains("frame size") || authEx.Message.Contains("corrupted frame"))
            {
                _logger.LogError("❌ Corrupted TLS frame detected - possible causes:");
                _logger.LogError("   - Client sending HTTPS to HTTP endpoint");
                _logger.LogError("   - TLS version mismatch");
                _logger.LogError("   - Network corruption");
                _logger.LogError("   - Firewall interference");
            }
        }

        if (exception.InnerException != null)
        {
            _logger.LogError("Inner Exception: {InnerExceptionType}: {InnerMessage}", 
                exception.InnerException.GetType().Name, exception.InnerException.Message);
        }

        // Suggest solutions
        var targetUri = new Uri(url);
        if (targetUri.Scheme == "http")
        {
            _logger.LogError("💡 Suggestion: Upstream is HTTP - ensure client connects via HTTP or configure SSL termination");
        }
        else
        {
            _logger.LogError("💡 Suggestions:");
            _logger.LogError("   - Check upstream server SSL certificate");
            _logger.LogError("   - Verify TLS version compatibility");
            _logger.LogError("   - Consider disabling certificate validation for development");
        }
    }

    private async Task<bool> TestPortConnectivityAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken);
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}

public class ConnectionDiagnostics
{
    public string TargetUrl { get; set; } = string.Empty;
    public TimeSpan DnsResolutionTime { get; set; }
    public List<string> ResolvedIpAddresses { get; set; } = new();
    public bool PingSuccess { get; set; }
    public long PingTime { get; set; }
    public bool PortOpen { get; set; }
    public string? Error { get; set; }
}
