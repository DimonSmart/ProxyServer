using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DimonSmart.ProxyServer.Services;

public interface ISslCertificateService
{
    X509Certificate2? LoadCertificate();
    X509Certificate2 CreateSelfSignedCertificate();
    bool ValidateCertificate(X509Certificate2 certificate);
}

public class SslCertificateService : ISslCertificateService
{
    private readonly ProxySettings _settings;
    private readonly ILogger<SslCertificateService> _logger;

    public SslCertificateService(ProxySettings settings, ILogger<SslCertificateService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public X509Certificate2? LoadCertificate()
    {
        if (string.IsNullOrEmpty(_settings.Ssl.CertificatePath))
        {
            _logger.LogDebug("No certificate path specified in settings");
            return null;
        }

        try
        {
            if (!File.Exists(_settings.Ssl.CertificatePath))
            {
                _logger.LogWarning("Certificate file not found at: {CertificatePath}", _settings.Ssl.CertificatePath);
                return null;
            }

            var certificate = new X509Certificate2(
                _settings.Ssl.CertificatePath, 
                _settings.Ssl.CertificatePassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            if (ValidateCertificate(certificate))
            {
                _logger.LogInformation("Successfully loaded SSL certificate from: {CertificatePath}", _settings.Ssl.CertificatePath);
                _logger.LogInformation("Certificate Subject: {Subject}", certificate.Subject);
                _logger.LogInformation("Certificate Valid From: {NotBefore} To: {NotAfter}", 
                    certificate.NotBefore, certificate.NotAfter);
                return certificate;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SSL certificate from: {CertificatePath}", _settings.Ssl.CertificatePath);
            return null;
        }
    }

    public X509Certificate2 CreateSelfSignedCertificate()
    {
        _logger.LogInformation("Creating self-signed certificate with subject: {Subject}", _settings.Ssl.SelfSignedCertificateSubject);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            _settings.Ssl.SelfSignedCertificateSubject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add Subject Alternative Names
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        // Add Key Usage
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Add Enhanced Key Usage
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                critical: true));

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        _logger.LogInformation("Self-signed certificate created successfully");
        _logger.LogInformation("Certificate Subject: {Subject}", certificate.Subject);
        _logger.LogInformation("Certificate Valid From: {NotBefore} To: {NotAfter}", 
            certificate.NotBefore, certificate.NotAfter);

        // Optionally save the certificate
        SaveSelfSignedCertificate(certificate);

        return certificate;
    }

    public bool ValidateCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                _logger.LogError("Certificate does not have a private key");
                return false;
            }

            // Check if certificate is expired
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore)
            {
                _logger.LogError("Certificate is not yet valid. Valid from: {NotBefore}", certificate.NotBefore);
                return false;
            }

            if (now > certificate.NotAfter)
            {
                _logger.LogError("Certificate has expired. Valid until: {NotAfter}", certificate.NotAfter);
                return false;
            }

            // Check if certificate is expiring soon (within 30 days)
            var expirationWarningDays = 30;
            if (now.AddDays(expirationWarningDays) > certificate.NotAfter)
            {
                var daysUntilExpiration = (certificate.NotAfter - now).Days;
                _logger.LogWarning("Certificate expires in {Days} days on {NotAfter}", 
                    daysUntilExpiration, certificate.NotAfter);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed");
            return false;
        }
    }

    private void SaveSelfSignedCertificate(X509Certificate2 certificate)
    {
        try
        {
            var certificatesDir = Path.Combine(Directory.GetCurrentDirectory(), "certificates");
            Directory.CreateDirectory(certificatesDir);

            var certificatePath = Path.Combine(certificatesDir, "self-signed.pfx");
            var certificatePassword = "ProxyServer123!";

            var pfxBytes = certificate.Export(X509ContentType.Pfx, certificatePassword);
            File.WriteAllBytes(certificatePath, pfxBytes);

            _logger.LogInformation("Self-signed certificate saved to: {CertificatePath}", certificatePath);
            _logger.LogInformation("Certificate password: {Password}", certificatePassword);
            _logger.LogInformation("You can configure this certificate in settings.json:");
            _logger.LogInformation("  \"Ssl\": {{");
            _logger.LogInformation("    \"CertificatePath\": \"{Path}\",", certificatePath.Replace("\\", "\\\\"));
            _logger.LogInformation("    \"CertificatePassword\": \"{Password}\"", certificatePassword);
            _logger.LogInformation("  }}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save self-signed certificate to disk");
        }
    }
}
