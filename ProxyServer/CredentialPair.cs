// Represents a pair of allowed IP patterns and allowed passwords.
namespace DimonSmart.ProxyServer;

public class CredentialPair
{
    public List<string> IPs { get; set; } = new();
    public List<string> Passwords { get; set; } = new();
}