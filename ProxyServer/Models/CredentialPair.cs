namespace DimonSmart.ProxyServer.Models;

public class CredentialPair
{
    public List<string> IPs { get; set; } = [];
    public List<string> Passwords { get; set; } = [];
}