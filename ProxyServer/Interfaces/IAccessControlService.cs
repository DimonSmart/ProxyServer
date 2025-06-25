namespace DimonSmart.ProxyServer.Interfaces;

public interface IAccessControlService
{
    (bool IsAllowed, int StatusCode, string? ErrorMessage) Validate(HttpContext context);
}
