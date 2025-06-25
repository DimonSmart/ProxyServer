namespace ProxyServer.FunctionalTests.Models;

/// <summary>
/// Represents a request to reverse a text string
/// </summary>
public class ReverseRequest
{
    /// <summary>
    /// The text to be reversed
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
