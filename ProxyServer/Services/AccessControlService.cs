using DimonSmart.ProxyServer.Interfaces;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Sockets.AddressFamily;

namespace DimonSmart.ProxyServer.Services;

public class AccessControlService : IAccessControlService
{
    private readonly ProxySettings _settings;
    private readonly ILogger<AccessControlService> _logger;

    public AccessControlService(ProxySettings settings, ILogger<AccessControlService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public (bool IsAllowed, int StatusCode, string? ErrorMessage) Validate(HttpContext context)
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        if (remoteIpAddress == null)
        {
            return (false, StatusCodes.Status403Forbidden, "Forbidden: Cannot determine remote IP");
        }

        // Use the original IP address without mapping
        var remoteIp = remoteIpAddress.ToString();
        _logger.LogInformation("Remote IP: {RemoteIP}, Is IPv6: {IsIPv6}", remoteIp, remoteIpAddress.AddressFamily == InterNetworkV6);

        string? providedPassword = null;
        if (context.Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
                try
                {
                    var decodedBytes = Convert.FromBase64String(encodedCredentials);
                    var decodedString = Encoding.UTF8.GetString(decodedBytes);
                    var parts = decodedString.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        // Username is ignored; only password is used.
                        providedPassword = parts[1];
                    }
                }
                catch { }
            }
        }

        var globalIpAllowed = false;
        var globalCredentialsAllowed = false;

        // If no credentials are configured, allow all requests.
        if (_settings.AllowedCredentials == null || _settings.AllowedCredentials.Count == 0)
        {
            _logger.LogDebug("No access control configured - allowing request from IP {RemoteIP}", remoteIp);
            return (true, 200, null);
        }

        // Check each credential pair.
        foreach (var pair in _settings.AllowedCredentials)
        {
            var currentIpAllowed = false;
            if (pair.IPs == null || pair.IPs.Count == 0)
            {
                currentIpAllowed = true;
                _logger.LogDebug("IP {RemoteIP}: No IP restrictions in this credential pair - allowing", remoteIp);
            }
            else
            {
                _logger.LogDebug("IP {RemoteIP}: Checking against patterns: {Patterns}", remoteIp, string.Join(", ", pair.IPs));
                foreach (var pattern in pair.IPs)
                {
                    if (IsMatch(remoteIp, pattern))
                    {
                        currentIpAllowed = true;
                        _logger.LogDebug("IP {RemoteIP}: Matched pattern '{Pattern}'", remoteIp, pattern);
                        break;
                    }
                    else
                    {
                        _logger.LogDebug("IP {RemoteIP}: Did not match pattern '{Pattern}'", remoteIp, pattern);
                    }
                }
            }

            if (currentIpAllowed)
            {
                globalIpAllowed = true;
                var currentPasswordAllowed = false;
                if (pair.Passwords == null || pair.Passwords.Count == 0)
                {
                    currentPasswordAllowed = true;
                }
                else if (!string.IsNullOrEmpty(providedPassword) && pair.Passwords.Contains(providedPassword))
                {
                    currentPasswordAllowed = true;
                }

                if (currentPasswordAllowed)
                {
                    globalCredentialsAllowed = true;
                    _logger.LogInformation("Access granted for IP {RemoteIP} - matched credential pair", remoteIp);
                    break;
                }
            }
        }

        if (!globalIpAllowed)
        {
            _logger.LogWarning("Access denied for IP {RemoteIP}: IP not in allowed list. Configured IP patterns: {AllowedIPs}",
                remoteIp,
                string.Join(", ", _settings.AllowedCredentials.SelectMany(c => c.IPs ?? new List<string>())));
            return (false, StatusCodes.Status403Forbidden, "Forbidden: IP not allowed");
        }
        if (!globalCredentialsAllowed)
        {
            _logger.LogWarning("Access denied for IP {RemoteIP}: Invalid credentials provided", remoteIp);
            return (false, StatusCodes.Status401Unauthorized, "Unauthorized: Invalid credentials");
        }
        return (true, 200, null);
    }

    // Helper method to match an IP address against a wildcard pattern (e.g. "192.168.*.*").
    private static bool IsMatch(string input, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(input, regexPattern);
    }
}