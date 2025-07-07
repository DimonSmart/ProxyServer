using System.Security.Cryptography.X509Certificates;

namespace DimonSmart.ProxyServer.Services;

public interface ISslCertificateService
{
    X509Certificate2? LoadCertificate();
    X509Certificate2 CreateSelfSignedCertificate();
    bool ValidateCertificate(X509Certificate2 certificate);
}
