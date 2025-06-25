namespace ProxyServer.FunctionalTests.Models;

/// <summary>
/// Represents a response containing the reversed text
/// </summary>
public class ReverseResponse
{
    /// <summary>
    /// The original text before reversal
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// The reversed text
    /// </summary>
    public string ReversedText { get; set; } = string.Empty;

    /// <summary>
    /// The sequential call number for this request
    /// </summary>
    public int CallNumber { get; set; }

    /// <summary>
    /// The timestamp when the request was processed
    /// </summary>
    public DateTime Timestamp { get; set; }
}
