using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DimonSmart.ProxyServer.Services;

public class SslCertificateService(ProxySettings settings, ILogger<SslCertificateService> logger) : ISslCertificateService
{
    private readonly ProxySettings _settings = settings;

    public X509Certificate2? LoadCertificate()
    {
        if (string.IsNullOrEmpty(_settings.Ssl.CertificatePath))
        {
            logger.LogDebug("No certificate path specified in settings");
            return null;
        }

        try
        {
            if (!File.Exists(_settings.Ssl.CertificatePath))
            {
                logger.LogWarning("Certificate file not found at: {CertificatePath}", _settings.Ssl.CertificatePath);
                return null;
            }

            var certificate = new X509Certificate2(
                _settings.Ssl.CertificatePath, 
                _settings.Ssl.CertificatePassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            if (ValidateCertificate(certificate))
            {
                logger.LogInformation("Successfully loaded SSL certificate from: {CertificatePath}", _settings.Ssl.CertificatePath);
                logger.LogInformation("Certificate Subject: {Subject}", certificate.Subject);
                logger.LogInformation("Certificate Valid From: {NotBefore} To: {NotAfter}", 
                    certificate.NotBefore, certificate.NotAfter);
                return certificate;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load SSL certificate from: {CertificatePath}", _settings.Ssl.CertificatePath);
            return null;
        }
    }

    public X509Certificate2 CreateSelfSignedCertificate()
    {
        logger.LogInformation("Creating self-signed certificate with subject: {Subject}", _settings.Ssl.SelfSignedCertificateSubject);

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

        logger.LogInformation("Self-signed certificate created successfully");
        logger.LogInformation("Certificate Subject: {Subject}", certificate.Subject);
        logger.LogInformation("Certificate Valid From: {NotBefore} To: {NotAfter}", 
            certificate.NotBefore, certificate.NotAfter);

        // Save and reload the certificate from disk
        var certificatePath = SaveSelfSignedCertificate(certificate);

        // Load the certificate from disk with proper key storage flags
        if (!string.IsNullOrEmpty(certificatePath) && File.Exists(certificatePath))
        {
            try
            {
                var diskCertificate = new X509Certificate2(
                    certificatePath, 
                    "ProxyServer123!");
                
                logger.LogInformation("Self-signed certificate reloaded from disk: {Path}", certificatePath);
                return diskCertificate;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reload certificate from disk, using in-memory certificate");
            }
        }

        return certificate;
    }

    public bool ValidateCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                logger.LogError("Certificate does not have a private key");
                return false;
            }

            // Check if certificate is expired
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore)
            {
                logger.LogError("Certificate is not yet valid. Valid from: {NotBefore}", certificate.NotBefore);
                return false;
            }

            if (now > certificate.NotAfter)
            {
                logger.LogError("Certificate has expired. Valid until: {NotAfter}", certificate.NotAfter);
                return false;
            }

            // Check if certificate is expiring soon (within 30 days)
            var expirationWarningDays = 30;
            if (now.AddDays(expirationWarningDays) > certificate.NotAfter)
            {
                var daysUntilExpiration = (certificate.NotAfter - now).Days;
                logger.LogWarning("Certificate expires in {Days} days on {NotAfter}", 
                    daysUntilExpiration, certificate.NotAfter);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Certificate validation failed");
            return false;
        }
    }

    private string? SaveSelfSignedCertificate(X509Certificate2 certificate)
    {
        try
        {
            var certificatesDir = Path.Combine(Directory.GetCurrentDirectory(), "certificates");
            Directory.CreateDirectory(certificatesDir);

            var certificatePath = Path.Combine(certificatesDir, "self-signed.pfx");
            var certificatePassword = "ProxyServer123!";

            var pfxBytes = certificate.Export(X509ContentType.Pfx, certificatePassword);
            File.WriteAllBytes(certificatePath, pfxBytes);

            logger.LogInformation("Self-signed certificate saved to: {CertificatePath}", certificatePath);
            logger.LogInformation("Certificate password: {Password}", certificatePassword);
            logger.LogInformation("You can configure this certificate in settings.json:");
            logger.LogInformation("  \"Ssl\": {{");
            logger.LogInformation("    \"CertificatePath\": \"{Path}\",", certificatePath.Replace("\\", "\\\\"));
            logger.LogInformation("    \"CertificatePassword\": \"{Password}\"", certificatePassword);
            logger.LogInformation("  }}");
            
            return certificatePath;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save self-signed certificate to disk");
            return null;
        }
    }
}
