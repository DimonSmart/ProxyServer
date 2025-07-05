using System.Security.Authentication;
using System.Text.Json.Serialization;

namespace DimonSmart.ProxyServer;

public class SslSettings
{
    /// <summary>
    /// Path to SSL certificate file (.pfx)
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for SSL certificate
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Whether to validate upstream server certificates (for HTTPS upstream)
    /// </summary>
    public bool ValidateUpstreamCertificate { get; set; } = true;

    /// <summary>
    /// Allowed SSL protocols for upstream connections (JSON configurable)
    /// Valid values: "None", "Ssl2", "Ssl3", "Tls", "Tls11", "Tls12", "Tls13"
    /// Can be combined with comma: "Tls12,Tls13"
    /// </summary>
    public string AllowedSslProtocolsString { get; set; } = "Tls12,Tls13";

    /// <summary>
    /// Parsed SSL protocols for upstream connections
    /// </summary>
    [JsonIgnore]
    public SslProtocols AllowedSslProtocols 
    { 
        get
        {
            if (string.IsNullOrWhiteSpace(AllowedSslProtocolsString))
                return SslProtocols.Tls12 | SslProtocols.Tls13;

            var protocols = SslProtocols.None;
            var parts = AllowedSslProtocolsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                if (Enum.TryParse<SslProtocols>(part.Trim(), true, out var protocol))
                {
                    protocols |= protocol;
                }
            }
            
            return protocols == SslProtocols.None ? SslProtocols.Tls12 | SslProtocols.Tls13 : protocols;
        }
    }

    /// <summary>
    /// Enable detailed SSL/TLS logging
    /// </summary>
    public bool EnableSslDebugging { get; set; } = false;

    /// <summary>
    /// Subject name for self-signed certificate generation
    /// </summary>
    public string SelfSignedCertificateSubject { get; set; } = "CN=ProxyServer";
}
